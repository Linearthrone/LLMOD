package com.housevictoria.remotecompanion

import android.os.Bundle
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.recyclerview.widget.GridLayoutManager
import com.housevictoria.remotecompanion.databinding.ActivityContactBookBinding
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class ContactBookActivity : AppCompatActivity() {
    private lateinit var binding: ActivityContactBookBinding
    private val apiClient = RemoteApiClient()
    private val uiScope = CoroutineScope(Job() + Dispatchers.Main)
    private var config = CompanionConfig("", "")
    private lateinit var adapter: ContactBookAdapter

    override fun onCreate(savedInstanceState: Bundle?) {
        ThemeManager.applyToActivity(this)
        super.onCreate(savedInstanceState)
        binding = ActivityContactBookBinding.inflate(layoutInflater)
        setContentView(binding.root)
        applyThemeColors()

        config = CompanionPrefs.load(this)
        adapter = ContactBookAdapter(config) { contact ->
            AppNavigation.openChat(this, contact, finishCurrent = false)
        }

        setSupportActionBar(binding.contactBookToolbar)
        binding.contactBookToolbar.setNavigationOnClickListener { AppNavigation.backSlide(this) }

        binding.contactGrid.layoutManager = GridLayoutManager(this, 2)
        binding.contactGrid.adapter = adapter

        binding.contactBookSwipeRefresh.setColorSchemeColors(getColor(R.color.hv_primary))
        binding.contactBookSwipeRefresh.setOnRefreshListener { loadContacts() }
        BottomNavHelper.wire(this, binding.bottomNav, R.id.nav_home)

        binding.contactBookRoot.alpha = 0f
        binding.contactBookRoot.translationY = 48f
        binding.contactBookRoot.animate().alpha(1f).translationY(0f).setDuration(360).start()

        loadContacts()
    }

    private fun applyThemeColors() {
        val palette = ThemeManager.currentPalette(this)
        binding.contactBookRoot.background = ThemeManager.meshBackground(palette)
        binding.contactBookToolbar.setTitleTextColor(palette.textPrimary)
        binding.contactBookToolbar.setSubtitleTextColor(palette.textSecondary)
        binding.contactBookSwipeRefresh.setColorSchemeColors(palette.primary)
    }

    override fun onDestroy() {
        uiScope.cancel()
        super.onDestroy()
    }

    private fun loadContacts() {
        val error = config.validate()
        if (error != null) {
            binding.contactBookSwipeRefresh.isRefreshing = false
            toast(error)
            return
        }

        binding.contactBookSwipeRefresh.isRefreshing = true
        uiScope.launch {
            val result = withContext(Dispatchers.IO) {
                runCatching { apiClient.listContacts(config.baseUrl, config.token) }
            }
            binding.contactBookSwipeRefresh.isRefreshing = false
            result.onSuccess { contacts ->
                adapter.submitList(contacts.sortedBy { it.name.lowercase() })
                if (contacts.isEmpty()) toast(getString(R.string.toast_no_contacts))
            }.onFailure { ex ->
                toast(ex.message ?: getString(R.string.status_error))
            }
        }
    }

    private fun toast(text: String) {
        Toast.makeText(this, text, Toast.LENGTH_SHORT).show()
    }
}
