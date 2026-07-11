package com.housevictoria.remotecompanion

import android.animation.ObjectAnimator
import android.os.Bundle
import android.view.Menu
import android.view.MenuItem
import android.view.View
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.isVisible
import androidx.recyclerview.widget.LinearLayoutManager
import com.housevictoria.remotecompanion.databinding.ActivityMainBinding
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class MainActivity : AppCompatActivity() {
    private lateinit var binding: ActivityMainBinding
    private val apiClient = RemoteApiClient()
    private val uiScope = CoroutineScope(Job() + Dispatchers.Main)
    private var config = CompanionConfig("", "")
    private lateinit var threadAdapter: ThreadAdapter
    private var systemStatusPollJob: Job? = null

    companion object {
        private const val SYSTEM_STATUS_POLL_INTERVAL_MS = 12_000L
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        ThemeManager.applyToActivity(this)
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)
        applyThemeColors()

        config = CompanionPrefs.load(this)
        threadAdapter = ThreadAdapter(config) { contact ->
            AppNavigation.openChat(this, contact)
        }

        setSupportActionBar(binding.inboxToolbar)
        binding.threadsRecycler.layoutManager = LinearLayoutManager(this)
        binding.threadsRecycler.adapter = threadAdapter
        binding.threadsRecycler.itemAnimator?.changeDuration = 220

        binding.inboxSwipeRefresh.setColorSchemeColors(getColor(R.color.hv_primary))
        binding.inboxSwipeRefresh.setOnRefreshListener { refreshInbox() }

        binding.openContactBookFab.setOnClickListener { AppNavigation.openContactBook(this) }
        BottomNavHelper.wire(this, binding.bottomNav, R.id.nav_home)

        binding.inboxRoot.alpha = 0f
        binding.inboxRoot.scaleX = 0.98f
        binding.inboxRoot.scaleY = 0.98f
        binding.inboxRoot.animate().alpha(1f).scaleX(1f).scaleY(1f).setDuration(420).start()

        pulseStatusDot()

        if (config.validate() != null) {
            AppNavigation.openSettings(this)
        } else {
            refreshInbox()
            loadSystemStatus()
        }
    }

    private fun applyThemeColors() {
        val palette = ThemeManager.currentPalette(this)
        binding.inboxRoot.background = ThemeManager.meshBackground(palette)
        binding.inboxToolbar.setTitleTextColor(palette.textPrimary)
        binding.inboxToolbar.setSubtitleTextColor(palette.textSecondary)
        binding.inboxStatusText.setTextColor(palette.textSecondary)
        binding.openContactBookFab.backgroundTintList =
            android.content.res.ColorStateList.valueOf(palette.primary)
        ThemeManager.strokeCard(binding.systemMonitorInclude.systemMonitorCard, palette)
        binding.systemMonitorInclude.systemMonitorTitle.setTextColor(palette.primary)
    }

    private fun loadSystemStatus() {
        val monitor = binding.systemMonitorInclude
        val summary = monitor.systemMonitorSummary
        val cpu = monitor.systemCpuText
        val gpu = monitor.systemGpuText
        val ram = monitor.systemRamText
        val uptime = monitor.systemUptimeText
        val servers = monitor.systemServersText

        uiScope.launch {
            val result = withContext(Dispatchers.IO) {
                runCatching { apiClient.getSystemStatus(config.baseUrl, config.token) }
            }
            result.onSuccess { status ->
                summary.text = getString(R.string.system_monitor_title)
                cpu.text = "${getString(R.string.system_cpu)}: ${status.cpuUsagePercent ?: 0}% · ${status.cpuTemperatureC ?: 0}°C"
                gpu.text = "${getString(R.string.system_gpu)}: ${status.gpuUsagePercent ?: 0}% · ${status.gpuTemperatureC ?: 0}°C"
                ram.text = "${getString(R.string.system_ram)}: ${status.ramUsedMb ?: 0}/${status.ramTotalMb ?: 0} MB (${status.ramUsagePercent ?: 0}%)"
                uptime.text = "${getString(R.string.system_uptime)}: ${status.uptimeLabel ?: "—"}"
                val serverLines = status.servers.orEmpty().joinToString("\n") { s ->
                    val mark = if (s.isRunning == true) "●" else "○"
                    "$mark ${s.name ?: "Server"}"
                }
                servers.text = serverLines.ifBlank { "—" }
            }.onFailure {
                summary.text = getString(R.string.system_unavailable)
            }
        }
    }

    override fun onResume() {
        super.onResume()
        config = CompanionPrefs.load(this)
        applyThemeColors()
        threadAdapter = ThreadAdapter(config) { contact ->
            AppNavigation.openChat(this, contact)
        }
        binding.threadsRecycler.adapter = threadAdapter
        if (config.validate() == null) refreshInbox()
        if (config.validate() == null) loadSystemStatus()
        startSystemStatusPolling()
    }

    override fun onPause() {
        stopSystemStatusPolling()
        super.onPause()
    }

    override fun onDestroy() {
        stopSystemStatusPolling()
        uiScope.cancel()
        super.onDestroy()
    }

    private fun startSystemStatusPolling() {
        stopSystemStatusPolling()
        if (config.validate() != null) return
        systemStatusPollJob = uiScope.launch {
            while (isActive) {
                delay(SYSTEM_STATUS_POLL_INTERVAL_MS)
                if (config.validate() == null) loadSystemStatus()
            }
        }
    }

    private fun stopSystemStatusPolling() {
        systemStatusPollJob?.cancel()
        systemStatusPollJob = null
    }

    override fun onCreateOptionsMenu(menu: Menu): Boolean {
        menuInflater.inflate(R.menu.menu_inbox, menu)
        return true
    }

    override fun onOptionsItemSelected(item: MenuItem): Boolean = when (item.itemId) {
        R.id.action_settings -> {
            AppNavigation.openSettings(this)
            true
        }
        R.id.action_contact_book -> {
            AppNavigation.openContactBook(this)
            true
        }
        else -> super.onOptionsItemSelected(item)
    }

    private fun refreshInbox() {
        val error = config.validate()
        if (error != null) {
            binding.inboxSwipeRefresh.isRefreshing = false
            updateConnection(ConnectionStatus.ERROR, error)
            binding.inboxEmptyState.isVisible = true
            binding.threadsRecycler.isVisible = false
            return
        }

        updateConnection(ConnectionStatus.CHECKING, getString(R.string.status_checking))
        binding.inboxSwipeRefresh.isRefreshing = true

        uiScope.launch {
            val result = withContext(Dispatchers.IO) {
                runCatching { apiClient.listContacts(config.baseUrl, config.token) }
            }

            binding.inboxSwipeRefresh.isRefreshing = false
            result.onSuccess { contacts ->
                val sorted = contacts.sortedByDescending { it.lastMessageAt.orEmpty() }
                threadAdapter.submitList(sorted)
                val hasThreads = sorted.isNotEmpty()
                binding.inboxEmptyState.isVisible = !hasThreads
                binding.threadsRecycler.isVisible = hasThreads
                updateConnection(ConnectionStatus.CONNECTED, getString(R.string.status_connected))
                if (!hasThreads) toast(getString(R.string.toast_no_contacts))
            }.onFailure { ex ->
                updateConnection(ConnectionStatus.ERROR, ex.message ?: getString(R.string.status_error))
                binding.inboxEmptyState.isVisible = true
                binding.threadsRecycler.isVisible = false
            }
        }
    }

    private fun updateConnection(status: ConnectionStatus, detail: String) {
        binding.inboxStatusText.text = detail
        binding.inboxStatusDot.setBackgroundResource(
            when (status) {
                ConnectionStatus.CONNECTED -> R.drawable.bg_status_dot_connected
                ConnectionStatus.CHECKING -> R.drawable.bg_status_dot_checking
                ConnectionStatus.ERROR -> R.drawable.bg_status_dot_error
                ConnectionStatus.UNKNOWN -> R.drawable.bg_status_dot_unknown
            }
        )
    }

    private fun pulseStatusDot() {
        ObjectAnimator.ofFloat(binding.inboxStatusDot, View.ALPHA, 0.45f, 1f).apply {
            duration = 900
            repeatCount = ObjectAnimator.INFINITE
            repeatMode = ObjectAnimator.REVERSE
            start()
        }
    }

    private fun toast(text: String) {
        Toast.makeText(this, text, Toast.LENGTH_SHORT).show()
    }
}
