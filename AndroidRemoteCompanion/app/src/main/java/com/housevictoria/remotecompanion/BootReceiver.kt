package com.housevictoria.remotecompanion

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent?) {
        if (intent?.action != Intent.ACTION_BOOT_COMPLETED) return
        if (CompanionPrefs.load(context).validate() != null) return
        NotificationScheduler.schedule(context)
    }
}
