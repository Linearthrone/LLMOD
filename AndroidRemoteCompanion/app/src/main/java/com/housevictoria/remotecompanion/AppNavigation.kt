package com.housevictoria.remotecompanion

import android.content.Intent
import androidx.appcompat.app.AppCompatActivity

object NavigationExtras {
    const val CONTACT_ID = "contact_id"
    const val CONTACT_NAME = "contact_name"
    const val CONTACT_DESCRIPTION = "contact_description"
    const val CONTACT_HAS_AVATAR = "contact_has_avatar"
}

object AppNavigation {
    fun openChat(activity: AppCompatActivity, contact: AiContact, finishCurrent: Boolean = false) {
        val intent = Intent(activity, ChatActivity::class.java).apply {
            putExtra(NavigationExtras.CONTACT_ID, contact.id)
            putExtra(NavigationExtras.CONTACT_NAME, contact.name)
            putExtra(NavigationExtras.CONTACT_DESCRIPTION, contact.description.orEmpty())
            putExtra(NavigationExtras.CONTACT_HAS_AVATAR, contact.hasAvatar)
        }
        activity.startActivity(intent)
        activity.overridePendingTransition(R.anim.slide_in_right, R.anim.slide_out_left)
        if (finishCurrent) activity.finish()
    }

    fun openContactBook(activity: AppCompatActivity) {
        activity.startActivity(Intent(activity, ContactBookActivity::class.java))
        activity.overridePendingTransition(R.anim.slide_in_up, R.anim.fade_out)
    }

    fun openSettings(activity: AppCompatActivity) {
        activity.startActivity(Intent(activity, SettingsActivity::class.java))
        activity.overridePendingTransition(R.anim.slide_in_up, R.anim.fade_out)
    }

    fun backSlide(activity: AppCompatActivity) {
        activity.finish()
        activity.overridePendingTransition(R.anim.slide_in_left, R.anim.slide_out_right)
    }
}
