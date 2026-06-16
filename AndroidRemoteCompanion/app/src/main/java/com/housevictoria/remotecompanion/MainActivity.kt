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
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class MainActivity : AppCompatActivity() {
    private lateinit var binding: ActivityMainBinding
    private val apiClient = RemoteApiClient()
    private val uiScope = CoroutineScope(Job() + Dispatchers.Main)
    private var config = CompanionConfig("", "")
    private lateinit var threadAdapter: ThreadAdapter

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

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

        binding.inboxRoot.alpha = 0f
        binding.inboxRoot.scaleX = 0.98f
        binding.inboxRoot.scaleY = 0.98f
        binding.inboxRoot.animate().alpha(1f).scaleX(1f).scaleY(1f).setDuration(420).start()

        pulseStatusDot()

        if (config.validate() != null) {
            AppNavigation.openSettings(this)
        } else {
            refreshInbox()
        }
    }

    override fun onResume() {
        super.onResume()
        config = CompanionPrefs.load(this)
        threadAdapter = ThreadAdapter(config) { contact ->
            AppNavigation.openChat(this, contact)
        }
        binding.threadsRecycler.adapter = threadAdapter
        if (config.validate() == null) refreshInbox()
    }

    override fun onDestroy() {
        uiScope.cancel()
        super.onDestroy()
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
