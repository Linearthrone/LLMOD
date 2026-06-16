package com.housevictoria.remotecompanion

import android.graphics.drawable.GradientDrawable
import android.os.Bundle
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.isVisible
import com.housevictoria.remotecompanion.databinding.ActivitySettingsBinding
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class SettingsActivity : AppCompatActivity() {
    private lateinit var binding: ActivitySettingsBinding
    private val apiClient = RemoteApiClient()
    private val uiScope = CoroutineScope(Job() + Dispatchers.Main)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivitySettingsBinding.inflate(layoutInflater)
        setContentView(binding.root)

        setSupportActionBar(binding.settingsToolbar)
        binding.settingsToolbar.setNavigationOnClickListener { AppNavigation.backSlide(this) }

        val config = CompanionPrefs.load(this)
        binding.settingsBaseUrl.setText(config.baseUrl)
        binding.settingsToken.setText(config.token)

        binding.settingsTestButton.setOnClickListener { testConnection() }
        binding.settingsSaveButton.setOnClickListener { saveSettings() }
    }

    override fun onDestroy() {
        uiScope.cancel()
        super.onDestroy()
    }

    private fun readDraft(): CompanionConfig = CompanionConfig(
        baseUrl = binding.settingsBaseUrl.text?.toString().orEmpty().trim(),
        token = binding.settingsToken.text?.toString().orEmpty().trim()
    )

    private fun testConnection() {
        val draft = readDraft()
        val error = draft.validate()
        if (error != null) {
            binding.settingsHealthResult.text = error
            binding.settingsHealthResult.setTextColor(getColor(R.color.hv_error))
            return
        }

        binding.settingsHealthResult.text = getString(R.string.status_checking)
        binding.settingsHealthResult.setTextColor(getColor(R.color.hv_text_secondary))
        binding.settingsTestButton.isEnabled = false

        uiScope.launch {
            val result = withContext(Dispatchers.IO) {
                runCatching { apiClient.checkHealth(draft.baseUrl) }
                    .getOrElse { "Health check failed: ${it.message}" }
            }
            binding.settingsTestButton.isEnabled = true
            val ok = result.startsWith("Healthy")
            binding.settingsHealthResult.text = result
            binding.settingsHealthResult.setTextColor(getColor(if (ok) R.color.hv_success else R.color.hv_error))
        }
    }

    private fun saveSettings() {
        val draft = readDraft()
        val error = draft.validate()
        if (error != null) {
            binding.settingsHealthResult.text = error
            binding.settingsHealthResult.setTextColor(getColor(R.color.hv_error))
            return
        }
        CompanionPrefs.save(this, draft)
        binding.settingsHealthResult.text = getString(R.string.toast_saved)
        binding.settingsHealthResult.setTextColor(getColor(R.color.hv_success))
        Toast.makeText(this, R.string.toast_saved, Toast.LENGTH_SHORT).show()
    }
}
