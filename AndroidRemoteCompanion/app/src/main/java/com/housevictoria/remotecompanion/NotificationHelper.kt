package com.housevictoria.remotecompanion

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat

object NotificationHelper {
    const val CHANNEL_ID = "victoria_messages"

    fun ensureChannel(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val manager = context.getSystemService(NotificationManager::class.java) ?: return
        val channel = NotificationChannel(
            CHANNEL_ID,
            context.getString(R.string.notification_channel_name),
            NotificationManager.IMPORTANCE_DEFAULT
        ).apply {
            description = context.getString(R.string.notification_channel_description)
        }
        manager.createNotificationChannel(channel)
    }

    fun showMessageNotification(context: Context, item: PendingNotificationItem) {
        if (!NotificationManagerCompat.from(context).areNotificationsEnabled()) return
        ensureChannel(context)

        val title = when (item.kind) {
            "unread_reminder" -> context.getString(R.string.notification_reminder_title)
            else -> item.contactName.ifBlank { context.getString(R.string.notification_default_title) }
        }
        val body = when (item.kind) {
            "unread_reminder" -> context.getString(R.string.notification_reminder_body)
            else -> item.preview.ifBlank { context.getString(R.string.notification_new_message_fallback) }
        }

        val intent = Intent(context, ChatActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
            putExtra(NavigationExtras.CONTACT_ID, item.safeContactId)
            putExtra(NavigationExtras.CONTACT_NAME, item.contactName.ifBlank { title })
            putExtra(NavigationExtras.CONTACT_DESCRIPTION, "")
            putExtra(NavigationExtras.CONTACT_HAS_AVATAR, false)
        }
        val pendingIntent = PendingIntent.getActivity(
            context,
            notificationRequestCode(item),
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val notification = NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_sparkle)
            .setContentTitle(title)
            .setContentText(body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(body))
            .setContentIntent(pendingIntent)
            .setAutoCancel(true)
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .build()

        NotificationManagerCompat.from(context).notify(notificationRequestCode(item), notification)
    }

    private fun notificationRequestCode(item: PendingNotificationItem): Int =
        (item.safeContactId + ":" + item.safeMessageId + ":" + item.safeKind).hashCode()
}
