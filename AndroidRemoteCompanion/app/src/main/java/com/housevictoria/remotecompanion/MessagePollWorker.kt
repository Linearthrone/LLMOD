package com.housevictoria.remotecompanion

import android.content.Context
import android.util.Log
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters

class MessagePollWorker(
    appContext: Context,
    params: WorkerParameters
) : CoroutineWorker(appContext, params) {

    override suspend fun doWork(): Result {
        val config = CompanionPrefs.load(applicationContext)
        if (config.validate() != null) {
            Log.d(TAG, "Skipping poll — connection settings invalid")
            return Result.success()
        }

        val client = RemoteApiClient()
        val pending = try {
            client.getPendingNotifications(config.baseUrl, config.token)
        } catch (ex: Exception) {
            Log.w(TAG, "Pending notifications poll failed: ${ex.message}")
            return Result.retry()
        }

        val items = pending.items.orEmpty()
        Log.d(TAG, "Pending notifications: ${items.size} item(s)")

        for (item in items) {
            val contactId = item.safeContactId
            val messageId = item.safeMessageId
            val kind = item.safeKind
            if (contactId.isBlank() || messageId.isBlank()) continue
            if (NotificationWatermarkPrefs.wasNotified(applicationContext, contactId, messageId, kind)) {
                continue
            }

            NotificationHelper.showMessageNotification(applicationContext, item)

            val ack = when (kind) {
                "unread_reminder" -> NotificationAck(
                    contactId = contactId,
                    ackReminderForMessageId = messageId
                )
                else -> NotificationAck(
                    contactId = contactId,
                    lastSeenMessageId = messageId
                )
            }

            try {
                client.ackNotifications(config.baseUrl, config.token, ack)
                NotificationWatermarkPrefs.markNotified(applicationContext, contactId, messageId, kind)
                Log.d(TAG, "Notified $kind for $contactId message $messageId")
            } catch (ex: Exception) {
                Log.w(TAG, "Ack failed for ${item.messageId}: ${ex.message}")
            }
        }

        return Result.success()
    }

    companion object {
        private const val TAG = "MessagePollWorker"
    }
}
