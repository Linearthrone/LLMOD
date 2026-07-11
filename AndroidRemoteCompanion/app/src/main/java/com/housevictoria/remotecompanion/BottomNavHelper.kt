package com.housevictoria.remotecompanion

import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import com.google.android.material.bottomnavigation.BottomNavigationView

object BottomNavHelper {
    fun wire(activity: AppCompatActivity, nav: BottomNavigationView, selectedId: Int) {
        val palette = ThemeManager.currentPalette(activity)
        nav.setBackgroundColor(palette.surface)
        nav.itemIconTintList = android.content.res.ColorStateList(
            arrayOf(
                intArrayOf(android.R.attr.state_checked),
                intArrayOf(-android.R.attr.state_checked)
            ),
            intArrayOf(palette.primary, palette.textMuted)
        )
        nav.itemTextColor = nav.itemIconTintList
        nav.selectedItemId = selectedId

        nav.setOnItemSelectedListener { item ->
            if (item.itemId == selectedId) return@setOnItemSelectedListener true
            when (item.itemId) {
                R.id.nav_home -> {
                    activity.startActivity(Intent(activity, MainActivity::class.java).apply {
                        flags = Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP
                    })
                    activity.finish()
                }
                R.id.nav_media_gen -> {
                    activity.startActivity(Intent(activity, MediaGenActivity::class.java))
                    activity.finish()
                }
                R.id.nav_gallery -> {
                    activity.startActivity(Intent(activity, GalleryActivity::class.java))
                    activity.finish()
                }
                R.id.nav_settings -> {
                    activity.startActivity(Intent(activity, SettingsActivity::class.java))
                    activity.finish()
                }
            }
            true
        }
    }
}
