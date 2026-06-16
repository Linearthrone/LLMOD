package com.housevictoria.remotecompanion

import android.Manifest
import android.content.pm.PackageManager
import android.graphics.drawable.GradientDrawable
import android.media.MediaRecorder
import android.os.Bundle
import android.os.SystemClock
import android.text.Editable
import android.text.TextWatcher
import android.view.HapticFeedbackConstants
import android.view.View
import android.view.animation.AnimationUtils
import android.view.inputmethod.EditorInfo
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.isVisible
import androidx.core.view.updatePadding
import androidx.recyclerview.widget.LinearLayoutManager
import com.google.android.material.snackbar.Snackbar
import com.housevictoria.remotecompanion.databinding.ActivityChatBinding
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File

class ChatActivity : AppCompatActivity() {
    private lateinit var binding: ActivityChatBinding
    private val apiClient = RemoteApiClient()
    private val uiScope = CoroutineScope(Job() + Dispatchers.Main)
    private val chatAdapter = ChatAdapter()
    private val messages = mutableListOf<ChatMessage>()

    private lateinit var contact: AiContact
    private lateinit var theme: ContactTheme
    private var config = CompanionConfig("", "")

    private var pendingRetryAction: (suspend () -> Unit)? = null
    private var isSubmitting = false
    private var recorder: MediaRecorder? = null
    private var recordingFilePath: String? = null
    private var recordingStartedAt = 0L
    private var recordingTimerJob: Job? = null

    private val recordAudioPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        if (granted) beginAudioRecording() else toast(getString(R.string.toast_mic_required))
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        WindowCompat.setDecorFitsSystemWindows(window, false)
        binding = ActivityChatBinding.inflate(layoutInflater)
        setContentView(binding.root)

        config = CompanionPrefs.load(this)
        contact = AiContact(
            id = intent.getStringExtra(NavigationExtras.CONTACT_ID).orEmpty(),
            name = intent.getStringExtra(NavigationExtras.CONTACT_NAME).orEmpty(),
            description = intent.getStringExtra(NavigationExtras.CONTACT_DESCRIPTION),
            hasAvatar = intent.getBooleanExtra(NavigationExtras.CONTACT_HAS_AVATAR, false)
        )
        theme = contact.theme()
        chatAdapter.setTheme(theme)
        chatAdapter.setAssistantName(contact.name)

        applyPersonaTheme()
        setupChrome()
        setupChatList()
        setupInputBar()
        setupRecording()

        binding.chatRoot.alpha = 0f
        binding.chatRoot.translationX = 56f
        binding.chatRoot.animate().alpha(1f).translationX(0f).setDuration(320).start()

        loadHistory()
    }

    override fun onDestroy() {
        stopRecorderSafely()
        recordingTimerJob?.cancel()
        uiScope.cancel()
        super.onDestroy()
    }

    private fun applyPersonaTheme() {
        binding.personaHeaderCard.strokeColor = theme.accent
        binding.personaHeaderGlow.setBackgroundColor(theme.accentGlow)
        binding.personaName.text = contact.name
        binding.personaName.setTextColor(theme.accentBright)
        binding.personaDescription.text = contact.description?.ifBlank { "AI persona" } ?: "AI persona"

        (binding.personaAvatarRing.background as? GradientDrawable)?.setStroke(
            (2.5f * resources.displayMetrics.density).toInt(),
            theme.avatarRing
        )

        AvatarLoader.load(this, binding.personaAvatar, config, contact.id, contact.hasAvatar, theme)
        binding.inputBarCard.strokeColor = theme.accent
        binding.micFab.backgroundTintList = android.content.res.ColorStateList.valueOf(theme.bubbleAssistant)
        binding.sendFab.backgroundTintList = android.content.res.ColorStateList.valueOf(theme.accent)
    }

    private fun setupChrome() {
        setSupportActionBar(binding.chatToolbar)
        binding.chatToolbar.setNavigationOnClickListener { AppNavigation.backSlide(this) }
        supportActionBar?.title = ""

        ViewCompat.setOnApplyWindowInsetsListener(binding.chatRoot) { _, insets ->
            val bars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            binding.inputBarCard.updatePadding(bottom = bars.bottom.coerceAtLeast(0))
            insets
        }
    }

    private fun setupChatList() {
        binding.chatRecycler.layoutManager = LinearLayoutManager(this).apply { stackFromEnd = true }
        binding.chatRecycler.adapter = chatAdapter
    }

    private fun setupInputBar() {
        binding.messageInput.addTextChangedListener(object : TextWatcher {
            override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) = Unit
            override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {
                val hasText = !s.isNullOrBlank()
                binding.sendFab.isVisible = hasText
                binding.micFab.isVisible = !hasText && recorder == null
            }
            override fun afterTextChanged(s: Editable?) = Unit
        })

        binding.messageInput.setOnEditorActionListener { _, actionId, _ ->
            if (actionId == EditorInfo.IME_ACTION_SEND) {
                sendTextMessage()
                true
            } else false
        }

        binding.sendFab.setOnClickListener { sendTextMessage() }
        binding.micFab.setOnClickListener { ensureMicPermissionAndStart() }
    }

    private fun setupRecording() {
        binding.stopRecordingButton.setOnClickListener { stopAndUploadRecording() }
    }

    private fun loadHistory() {
        val error = config.validate()
        if (error != null) {
            toast(getString(R.string.toast_config_required))
            binding.personaStatusLabel.text = getString(R.string.persona_offline)
            binding.personaOnlineDot.setBackgroundResource(R.drawable.bg_status_dot_error)
            return
        }

        uiScope.launch {
            val result = withContext(Dispatchers.IO) {
                runCatching { apiClient.getMessages(config.baseUrl, config.token, contact.id) }
            }
            result.onSuccess { remote ->
                messages.clear()
                messages.addAll(remote.map { it.toChatMessage() })
                publishMessages()
                binding.personaStatusLabel.text = getString(R.string.persona_online)
            }.onFailure {
                binding.personaStatusLabel.text = getString(R.string.persona_offline)
                binding.personaOnlineDot.setBackgroundResource(R.drawable.bg_status_dot_error)
            }
        }
    }

    private fun sendTextMessage() {
        if (isSubmitting) return
        if (config.validate() != null) {
            toast(getString(R.string.toast_config_required))
            return
        }

        val msg = binding.messageInput.text?.toString().orEmpty().trim()
        if (msg.isBlank()) {
            toast(getString(R.string.toast_message_required))
            return
        }

        binding.messageInput.performHapticFeedback(HapticFeedbackConstants.CONFIRM)
        addMessage(MessageRole.USER, msg)
        binding.messageInput.setText("")
        showTypingIndicator()

        val request = config
        val contactId = contact.id
        runRequestWithRetry({
            val outcome = withContext(Dispatchers.IO) {
                apiClient.sendChat(request.baseUrl, request.token, msg, contactId)
            }
            removeTypingIndicator()
            handleOutcome(outcome)
        })
    }

    private fun ensureMicPermissionAndStart() {
        if (isSubmitting) return
        if (config.validate() != null) {
            toast(getString(R.string.toast_config_required))
            return
        }
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) {
            beginAudioRecording()
        } else {
            recordAudioPermissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
        }
    }

    private fun beginAudioRecording() {
        if (recorder != null) return
        val file = runCatching { createTempFile("rc_audio_", ".3gp", cacheDir) }.getOrNull() ?: return

        val localRecorder = runCatching {
            MediaRecorder().apply {
                setAudioSource(MediaRecorder.AudioSource.MIC)
                setOutputFormat(MediaRecorder.OutputFormat.THREE_GPP)
                setAudioEncoder(MediaRecorder.AudioEncoder.AMR_NB)
                setOutputFile(file.absolutePath)
                prepare()
                start()
            }
        }.getOrElse {
            toast("Failed to start recording: ${it.message}")
            return
        }

        recorder = localRecorder
        recordingFilePath = file.absolutePath
        recordingStartedAt = SystemClock.elapsedRealtime()
        binding.micFab.isVisible = false
        binding.sendFab.isVisible = false
        binding.recordingOverlay.isVisible = true
        binding.recordingPulseDot.startAnimation(AnimationUtils.loadAnimation(this, R.anim.pulse_recording))
        startRecordingTimer()
    }

    private fun startRecordingTimer() {
        recordingTimerJob?.cancel()
        recordingTimerJob = uiScope.launch {
            while (isActive && recorder != null) {
                val elapsed = SystemClock.elapsedRealtime() - recordingStartedAt
                val seconds = (elapsed / 1000).toInt()
                binding.recordingTimerText.text = String.format("%d:%02d · tap stop when done", seconds / 60, seconds % 60)
                delay(250)
            }
        }
    }

    private fun stopAndUploadRecording() {
        stopRecorderSafely()
        recordingTimerJob?.cancel()
        binding.recordingOverlay.isVisible = false
        binding.recordingPulseDot.clearAnimation()
        binding.micFab.isVisible = binding.messageInput.text.isNullOrBlank()

        val path = recordingFilePath ?: return
        val audioFile = File(path)
        if (!audioFile.exists() || audioFile.length() == 0L) return
        recordingFilePath = null

        addMessage(MessageRole.USER, "🎤 Voice message")
        showTypingIndicator()

        val request = config
        val contactId = contact.id
        runRequestWithRetry({
            val outcome = withContext(Dispatchers.IO) {
                apiClient.sendChatAudio(request.baseUrl, request.token, audioFile, contactId)
            }
            removeTypingIndicator()
            handleOutcome(outcome)
        })
    }

    private fun stopRecorderSafely() {
        recorder?.let {
            runCatching { it.stop() }
            runCatching { it.reset() }
            runCatching { it.release() }
        }
        recorder = null
    }

    private fun runRequestWithRetry(action: suspend () -> Unit) {
        pendingRetryAction = action
        setSubmitting(true)
        uiScope.launch {
            val result = runCatching { action.invoke() }
            setSubmitting(false)
            result.exceptionOrNull()?.let { ex ->
                addMessage(MessageRole.ERROR, "Network error: ${ex.message}")
                Snackbar.make(binding.chatRoot, ex.message ?: "Request failed", Snackbar.LENGTH_LONG)
                    .setAction(getString(R.string.snackbar_retry)) {
                        pendingRetryAction?.let { retry ->
                            showTypingIndicator()
                            runRequestWithRetry(retry)
                        }
                    }
                    .setActionTextColor(theme.accentBright)
                    .show()
            }
        }
    }

    private fun handleOutcome(outcome: ChatOutcome) {
        if (outcome.ok) {
            pendingRetryAction = null
            addMessage(MessageRole.ASSISTANT, outcome.text, outcome.conversationId)
        } else {
            addMessage(MessageRole.ERROR, outcome.text)
        }
    }

    private fun showTypingIndicator() {
        if (messages.any { it.id == TYPING_ID }) return
        messages.add(ChatMessage(id = TYPING_ID, role = MessageRole.TYPING, content = ""))
        publishMessages()
    }

    private fun removeTypingIndicator() {
        if (messages.removeAll { it.id == TYPING_ID }) publishMessages()
    }

    private fun addMessage(role: MessageRole, content: String, conversationId: String? = null) {
        messages.add(ChatMessage(role = role, content = content, conversationId = conversationId))
        publishMessages()
    }

    private fun publishMessages() {
        chatAdapter.submitList(messages.toList()) { scrollToBottom() }
    }

    private fun scrollToBottom() {
        if (messages.isEmpty()) return
        binding.chatRecycler.post {
            binding.chatRecycler.smoothScrollToPosition(messages.lastIndex.coerceAtLeast(0))
        }
    }

    private fun setSubmitting(submitting: Boolean) {
        isSubmitting = submitting
        binding.messageInput.isEnabled = !submitting
        if (recorder == null) {
            binding.micFab.isEnabled = !submitting
            binding.sendFab.isEnabled = !submitting
        }
    }

    private fun toast(text: String) {
        Toast.makeText(this, text, Toast.LENGTH_SHORT).show()
    }

    companion object {
        private const val TYPING_ID = "typing-indicator"
    }
}
