package com.housevictoria.remotecompanion

import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import androidx.fragment.app.Fragment
import androidx.viewpager2.adapter.FragmentStateAdapter
import com.google.android.material.tabs.TabLayoutMediator
import com.housevictoria.remotecompanion.databinding.ActivitySettingsBinding

class SettingsActivity : AppCompatActivity() {
    private lateinit var binding: ActivitySettingsBinding

    override fun onCreate(savedInstanceState: Bundle?) {
        ThemeManager.applyToActivity(this)
        super.onCreate(savedInstanceState)
        binding = ActivitySettingsBinding.inflate(layoutInflater)
        setContentView(binding.root)

        applyThemeColors()

        setSupportActionBar(binding.settingsToolbar)
        binding.settingsToolbar.setNavigationOnClickListener { AppNavigation.backSlide(this) }

        binding.settingsPager.adapter = SettingsPagerAdapter(this)
        TabLayoutMediator(binding.settingsTabs, binding.settingsPager) { tab, position ->
            tab.text = when (position) {
                0 -> getString(R.string.settings_tab_connection)
                else -> getString(R.string.settings_tab_themes)
            }
        }.attach()

        BottomNavHelper.wire(this, binding.bottomNav, R.id.nav_settings)
    }

    private fun applyThemeColors() {
        val palette = ThemeManager.currentPalette(this)
        binding.settingsRoot.background = ThemeManager.meshBackground(palette)
        binding.settingsToolbar.setTitleTextColor(palette.textPrimary)
        binding.settingsToolbar.setSubtitleTextColor(palette.textSecondary)
    }

    private class SettingsPagerAdapter(activity: AppCompatActivity) : FragmentStateAdapter(activity) {
        override fun getItemCount(): Int = 2
        override fun createFragment(position: Int): Fragment = when (position) {
            0 -> SettingsConnectionFragment()
            else -> SettingsThemesFragment()
        }
    }
}
