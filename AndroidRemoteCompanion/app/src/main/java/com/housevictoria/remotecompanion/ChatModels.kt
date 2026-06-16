package com.housevictoria.remotecompanion

import android.content.Context
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.UUID
import java.util.concurrent.TimeUnit

enum class MessageRole {
    USER,
    ASSISTANT,
    SYSTEM,
    ERROR,
    TYPING
}

data class ChatMessage(
    val id: String = UUID.randomUUID().toString(),
    val role: MessageRole,
    val content: String,
    val timestamp: Long = System.currentTimeMillis(),
    val conversationId: String? = null
) {
    fun formattedTime(): String =
        SimpleDateFormat("h:mm a", Locale.getDefault()).format(Date(timestamp))
}

data class RemoteMessage(
    val id: String,
    val role: MessageRole,
    val content: String,
    val timestampMs: Long
) {
    fun toChatMessage(): ChatMessage = ChatMessage(
        id = id.ifBlank { UUID.randomUUID().toString() },
        role = role,
        content = content,
        timestamp = timestampMs
    )
}

data class AiContact(
    val id: String,
    val name: String,
    val description: String? = null,
    val isPrimary: Boolean = false,
    val hasAvatar: Boolean = false,
    val lastMessagePreview: String? = null,
    val lastMessageAt: String? = null
) {
    fun theme(): ContactTheme = ContactThemePalette.forContact(id)

    fun relativeTimeLabel(): String {
        val raw = lastMessageAt ?: return ""
        val ms = runCatching { java.time.Instant.parse(raw).toEpochMilli() }.getOrElse {
            runCatching {
                SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", Locale.US)
                    .parse(raw.take(19))?.time
            }.getOrNull() ?: return ""
        }
        val delta = System.currentTimeMillis() - ms
        return when {
            delta < TimeUnit.MINUTES.toMillis(1) -> "now"
            delta < TimeUnit.HOURS.toMillis(1) -> "${TimeUnit.MILLISECONDS.toMinutes(delta)}m"
            delta < TimeUnit.DAYS.toMillis(1) -> "${TimeUnit.MILLISECONDS.toHours(delta)}h"
            delta < TimeUnit.DAYS.toMillis(7) -> "${TimeUnit.MILLISECONDS.toDays(delta)}d"
            else -> SimpleDateFormat("MMM d", Locale.getDefault()).format(Date(ms))
        }
    }
}

data class CompanionConfig(
    val baseUrl: String,
    val token: String
) {
    fun validate(): String? {
        if (baseUrl.isBlank()) return "Base URL is required."
        if (!(baseUrl.startsWith("http://") || baseUrl.startsWith("https://"))) {
            return "Base URL must start with http:// or https://"
        }
        if (token.length < 16) return "Token must be at least 16 characters."
        return null
    }
}

object CompanionPrefs {
    private const val PREFS = "remote_companion_prefs"
    private const val KEY_BASE_URL = "base_url"
    private const val KEY_TOKEN = "token"

    fun load(context: Context): CompanionConfig {
        val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        return CompanionConfig(
            baseUrl = prefs.getString(KEY_BASE_URL, "http://127.0.0.1:17890").orEmpty(),
            token = prefs.getString(KEY_TOKEN, "").orEmpty()
        )
    }

    fun save(context: Context, config: CompanionConfig) {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .putString(KEY_BASE_URL, config.baseUrl.trim())
            .putString(KEY_TOKEN, config.token.trim())
            .apply()
    }
}

enum class ConnectionStatus {
    UNKNOWN,
    CHECKING,
    CONNECTED,
    ERROR
}
