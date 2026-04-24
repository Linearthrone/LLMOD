package com.housevictoria.remotecompanion

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.media.MediaRecorder
import android.os.Bundle
import android.view.View
import android.widget.Button
import android.widget.TextView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.ContextCompat
import androidx.appcompat.app.AppCompatActivity
import com.google.android.material.textfield.TextInputEditText
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class MainActivity : AppCompatActivity() {
    private val apiClient = RemoteApiClient()
    private val uiScope = CoroutineScope(Job() + Dispatchers.Main)
    private val history = mutableListOf<String>()
    private var pendingRetryAction: (suspend () -> Unit)? = null
    private var isSubmitting = false
    private var lastRequestHadApiError = false

    private lateinit var baseUrlInput: TextInputEditText
    private lateinit var tokenInput: TextInputEditText
    private lateinit var contactIdInput: TextInputEditText
    private lateinit var messageInput: TextInputEditText
    private lateinit var healthResultText: TextView
    private lateinit var conversationText: TextView
    private lateinit var sendButton: Button
    private lateinit var audioButton: Button
    private lateinit var retryButton: Button
    private var recorder: MediaRecorder? = null
    private var recordingFilePath: String? = null

    private val recordAudioPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        if (granted) {
            beginAudioRecording()
        } else {
            toast("Microphone permission is required for audio chat.")
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        baseUrlInput = findViewById(R.id.baseUrlInput)
        tokenInput = findViewById(R.id.tokenInput)
        contactIdInput = findViewById(R.id.contactIdInput)
        messageInput = findViewById(R.id.messageInput)
        healthResultText = findViewById(R.id.healthResultText)
        conversationText = findViewById(R.id.conversationText)
        sendButton = findViewById(R.id.sendButton)
        audioButton = findViewById(R.id.audioButton)
        retryButton = findViewById(R.id.retryButton)

        val prefs = getSharedPreferences("remote_companion_prefs", Context.MODE_PRIVATE)
        baseUrlInput.setText(prefs.getString(KEY_BASE_URL, "http://127.0.0.1:17890"))
        tokenInput.setText(prefs.getString(KEY_TOKEN, ""))
        contactIdInput.setText(prefs.getString(KEY_CONTACT_ID, ""))

        findViewById<Button>(R.id.healthButton).setOnClickListener {
            val baseUrl = baseUrlInput.text?.toString().orEmpty().trim()
            if (baseUrl.isBlank()) {
                toast("Base URL is required")
                return@setOnClickListener
            }
            savePrefs()
            healthResultText.text = "Health: checking..."
            uiScope.launch {
                val result = withContext(Dispatchers.IO) {
                    runCatching { apiClient.checkHealth(baseUrl) }
                        .getOrElse { "Health check failed: ${it.message}" }
                }
                healthResultText.text = result
            }
        }

        sendButton.setOnClickListener {
            val configError = validateConfig()
            if (configError != null) {
                toast(configError)
                return@setOnClickListener
            }

            val msg = messageInput.text?.toString().orEmpty().trim()
            if (msg.isBlank()) {
                toast("Message is required")
                return@setOnClickListener
            }

            savePrefs()
            appendHistory("user", msg)
            setLoadingState(true, "Sending text message...")
            val baseUrl = baseUrlInput.text?.toString().orEmpty().trim()
            val token = tokenInput.text?.toString().orEmpty().trim()
            val contactId = contactIdInput.text?.toString().orEmpty().trim()
            runRequestWithRetry({
                val outcome = withContext(Dispatchers.IO) {
                    apiClient.sendChat(baseUrl, token, msg, contactId)
                }
                handleOutcome(outcome)
                messageInput.setText("")
            }, "Text message")
        }

        audioButton.setOnClickListener {
            if (isSubmitting) return@setOnClickListener
            val configError = validateConfig()
            if (configError != null) {
                toast(configError)
                return@setOnClickListener
            }

            if (recorder == null) {
                ensureMicPermissionAndStart()
            } else {
                stopAndUploadRecording()
            }
        }

        retryButton.setOnClickListener {
            val retry = pendingRetryAction ?: return@setOnClickListener
            setLoadingState(true, "Retrying request...")
            uiScope.launch {
                retry.invoke()
            }
        }
    }

    override fun onDestroy() {
        stopRecorderSafely()
        super.onDestroy()
        uiScope.cancel()
    }

    private fun savePrefs() {
        val prefs = getSharedPreferences("remote_companion_prefs", Context.MODE_PRIVATE)
        prefs.edit()
            .putString(KEY_BASE_URL, baseUrlInput.text?.toString().orEmpty().trim())
            .putString(KEY_TOKEN, tokenInput.text?.toString().orEmpty().trim())
            .putString(KEY_CONTACT_ID, contactIdInput.text?.toString().orEmpty().trim())
            .apply()
    }

    private fun validateConfig(): String? {
        val baseUrl = baseUrlInput.text?.toString().orEmpty().trim()
        val token = tokenInput.text?.toString().orEmpty().trim()
        if (baseUrl.isBlank()) return "Base URL is required."
        if (!(baseUrl.startsWith("http://") || baseUrl.startsWith("https://"))) {
            return "Base URL must start with http:// or https://"
        }
        if (token.length < 16) return "Token must be at least 16 characters."
        return null
    }

    private fun ensureMicPermissionAndStart() {
        val hasPermission = ContextCompat.checkSelfPermission(
            this,
            Manifest.permission.RECORD_AUDIO
        ) == PackageManager.PERMISSION_GRANTED

        if (hasPermission) {
            beginAudioRecording()
        } else {
            recordAudioPermissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
        }
    }

    private fun beginAudioRecording() {
        if (recorder != null) return

        val file = kotlin.runCatching {
            createTempFile("rc_audio_", ".3gp", cacheDir)
        }.getOrNull()

        if (file == null) {
            toast("Failed to create temporary audio file.")
            return
        }

        val localRecorder = kotlin.runCatching {
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
        audioButton.text = getString(R.string.button_audio_stop)
        appendHistory("system", "Recording started...")
    }

    private fun stopAndUploadRecording() {
        stopRecorderSafely()
        audioButton.text = getString(R.string.button_audio)

        val recordedPath = recordingFilePath
        if (recordedPath.isNullOrBlank()) {
            toast("No audio recording found.")
            return
        }

        val audioFile = java.io.File(recordedPath)
        if (!audioFile.exists() || audioFile.length() == 0L) {
            toast("Recorded audio is empty.")
            return
        }
        recordingFilePath = null

        savePrefs()
        appendHistory("user", "[audio] ${audioFile.length()} bytes")
        setLoadingState(true, "Uploading audio...")

        val baseUrl = baseUrlInput.text?.toString().orEmpty().trim()
        val token = tokenInput.text?.toString().orEmpty().trim()
        val contactId = contactIdInput.text?.toString().orEmpty().trim()
        runRequestWithRetry({
            val outcome = withContext(Dispatchers.IO) {
                apiClient.sendChatAudio(baseUrl, token, audioFile, contactId)
            }
            handleOutcome(outcome)
        }, "Audio upload")
    }

    private fun stopRecorderSafely() {
        recorder?.let { current ->
            kotlin.runCatching { current.stop() }
            kotlin.runCatching { current.reset() }
            kotlin.runCatching { current.release() }
        }
        recorder = null
    }

    private fun runRequestWithRetry(
        action: suspend () -> Unit,
        requestName: String
    ) {
        pendingRetryAction = action
        lastRequestHadApiError = false
        uiScope.launch {
            val result = kotlin.runCatching {
                action.invoke()
            }

            result.exceptionOrNull()?.let { ex ->
                val message = "Network error during $requestName: ${ex.message ?: "unknown error"}"
                appendHistory("error", message)
                pendingRetryAction = action
                retryButton.isEnabled = true
                retryButton.visibility = View.VISIBLE
                setLoadingState(false, "Request failed.")
            } ?: run {
                if (lastRequestHadApiError) {
                    retryButton.visibility = View.VISIBLE
                } else {
                    pendingRetryAction = null
                    retryButton.visibility = View.GONE
                }
                setLoadingState(false, "Done.")
            }
        }
    }

    private fun handleOutcome(outcome: ChatOutcome) {
        if (outcome.ok) {
            appendHistory("assistant", outcome.text + formatConversationId(outcome.conversationId))
        } else {
            lastRequestHadApiError = true
            appendHistory("error", outcome.text)
            retryButton.visibility = View.VISIBLE
        }
    }

    private fun formatConversationId(conversationId: String?): String {
        return if (conversationId.isNullOrBlank()) "" else "\nconversationId: $conversationId"
    }

    private fun appendHistory(role: String, message: String) {
        val prefix = when (role) {
            "user" -> "You"
            "assistant" -> "Assistant"
            "system" -> "System"
            "error" -> "Error"
            else -> role
        }
        history += "$prefix: $message"
        conversationText.text = history.joinToString("\n\n")
    }

    private fun setLoadingState(loading: Boolean, status: String) {
        isSubmitting = loading
        healthResultText.text = if (loading) status else getString(R.string.health_idle)
        sendButton.isEnabled = !loading
        retryButton.isEnabled = !loading
        if (recorder == null) {
            audioButton.isEnabled = !loading
        }
    }

    private fun toast(text: String) {
        Toast.makeText(this, text, Toast.LENGTH_SHORT).show()
    }

    companion object {
        private const val KEY_BASE_URL = "base_url"
        private const val KEY_TOKEN = "token"
        private const val KEY_CONTACT_ID = "contact_id"
    }
}
