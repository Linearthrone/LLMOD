package com.housevictoria.remotecompanion

import android.content.Context

object NotificationWatermarkPrefs {
    private const val PREFS = "notification_watermarks"

    private fun key(contactId: String, messageId: String, kind: String): String =
        "$contactId|$messageId|$kind"

    fun wasNotified(context: Context, contactId: String, messageId: String, kind: String): Boolean {
        val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        return prefs.getBoolean(key(contactId, messageId, kind), false)
    }

    fun markNotified(context: Context, contactId: String, messageId: String, kind: String) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .putBoolean(key(contactId, messageId, kind), true)
            .apply()
    }
}
