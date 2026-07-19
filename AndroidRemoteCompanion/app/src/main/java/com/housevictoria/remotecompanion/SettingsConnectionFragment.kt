package com.housevictoria.remotecompanion

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import android.widget.Toast
import androidx.fragment.app.Fragment
import com.google.android.material.button.MaterialButton
import com.google.android.material.card.MaterialCardView
import com.google.android.material.textfield.TextInputLayout
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class SettingsConnectionFragment : Fragment() {
    private val apiClient = RemoteApiClient()
    private val uiScope = CoroutineScope(Job() + Dispatchers.Main)
    private var baseUrlField: com.google.android.material.textfield.TextInputEditText? = null
    private var tokenField: com.google.android.material.textfield.TextInputEditText? = null
    private var healthResult: TextView? = null

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View = inflater.inflate(R.layout.page_settings_connection, container, false)

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        baseUrlField = view.findViewById(R.id.settingsBaseUrl)
        tokenField = view.findViewById(R.id.settingsToken)
        healthResult = view.findViewById(R.id.settingsHealthResult)

        val palette = ThemeManager.currentPalette(requireContext())
        view.findViewById<MaterialCardView>(R.id.settingsConnectionCard)?.let {
            ThemeManager.applyCard(it, palette)
        }
        listOf(
            baseUrlField?.parent as? TextInputLayout,
            tokenField?.parent as? TextInputLayout
        ).forEach { layout ->
            layout?.let { ThemeManager.applyInputLayout(it, palette) }
        }
        view.findViewById<TextView>(R.id.settingsHelpText)?.let {
            ThemeManager.applyText(it, palette, muted = true)
        }
        healthResult?.let { ThemeManager.applyText(it, palette, secondary = true) }
        view.findViewById<MaterialButton>(R.id.settingsTestButton)?.let {
            ThemeManager.applyOutlinedButton(it, palette)
        }
        view.findViewById<MaterialButton>(R.id.settingsSaveButton)?.let {
            ThemeManager.applyFilledButton(it, palette)
        }

        val config = CompanionPrefs.load(requireContext())
        baseUrlField?.setText(config.baseUrl)
        tokenField?.setText(config.token)

        view.findViewById<MaterialButton>(R.id.settingsTestButton)
            ?.setOnClickListener { testConnection() }
        view.findViewById<MaterialButton>(R.id.settingsSaveButton)
            ?.setOnClickListener { saveSettings() }
    }

    override fun onDestroyView() {
        uiScope.cancel()
        super.onDestroyView()
    }

    private fun readDraft(): CompanionConfig = CompanionConfig(
        baseUrl = baseUrlField?.text?.toString().orEmpty().trim(),
        token = tokenField?.text?.toString().orEmpty().trim()
    )

    private fun testConnection() {
        val draft = readDraft()
        val error = draft.validate()
        val palette = ThemeManager.currentPalette(requireContext())
        if (error != null) {
            healthResult?.text = error
            healthResult?.setTextColor(palette.error)
            return
        }
        healthResult?.text = getString(R.string.status_checking)
        healthResult?.setTextColor(palette.textSecondary)
        uiScope.launch {
            val result = withContext(Dispatchers.IO) {
                runCatching { apiClient.checkHealth(draft.baseUrl) }
                    .getOrElse { "Health check failed: ${it.message}" }
            }
            val ok = result.startsWith("Healthy")
            healthResult?.text = result
            healthResult?.setTextColor(if (ok) palette.success else palette.error)
        }
    }

    private fun saveSettings() {
        val draft = readDraft()
        val error = draft.validate()
        val palette = ThemeManager.currentPalette(requireContext())
        if (error != null) {
            healthResult?.text = error
            healthResult?.setTextColor(palette.error)
            return
        }
        CompanionPrefs.save(requireContext(), draft)
        NotificationHelper.ensureChannel(requireContext())
        NotificationScheduler.schedule(requireContext())
        healthResult?.text = getString(R.string.toast_saved)
        healthResult?.setTextColor(palette.success)
        Toast.makeText(requireContext(), R.string.toast_saved, Toast.LENGTH_SHORT).show()
    }
}
