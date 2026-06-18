package com.housevictoria.remotecompanion

import com.squareup.moshi.Json
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.asRequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.File
import java.io.IOException
import java.util.concurrent.TimeUnit

class RemoteApiClient {
    // LLM replies can take well over OkHttp's default 10s read timeout (especially over Tailscale).
    private val http = OkHttpClient.Builder()
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(5, TimeUnit.MINUTES)
        .writeTimeout(2, TimeUnit.MINUTES)
        .build()
    private val moshi = Moshi.Builder().add(KotlinJsonAdapterFactory()).build()
    private val chatReqAdapter = moshi.adapter(ChatRequest::class.java)
    private val chatRespAdapter = moshi.adapter(ChatResponse::class.java)
    private val healthRespAdapter = moshi.adapter(HealthResponse::class.java)
    private val errRespAdapter = moshi.adapter(ErrorResponse::class.java)
    private val contactsRespAdapter = moshi.adapter(ContactsResponse::class.java)
    private val messagesRespAdapter = moshi.adapter(MessagesResponse::class.java)
    private val jsonMediaType = "application/json; charset=utf-8".toMediaType()

    @Throws(IOException::class)
    fun checkHealth(baseUrl: String): String {
        val url = "${baseUrl.trimEnd('/')}/api/remote/v1/health"
        val req = Request.Builder().url(url).get().build()
        http.newCall(req).execute().use { resp ->
            val body = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) return "HTTP ${resp.code}: $body"
            val parsed = healthRespAdapter.fromJson(body)
            return if (parsed?.ok == true) "Healthy (${parsed.service ?: "service"})" else "Unexpected response: $body"
        }
    }

    @Throws(IOException::class)
    fun listContacts(baseUrl: String, token: String): List<AiContact> {
        val url = "${baseUrl.trimEnd('/')}/api/remote/v1/contacts"
        val req = authorizedRequest(url, token).get().build()
        http.newCall(req).execute().use { resp ->
            val body = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) throw IOException(parseErrorText(resp.code, body))
            val parsed = contactsRespAdapter.fromJson(body)
            return parsed?.contacts.orEmpty().map { it.toAiContact() }
        }
    }

    @Throws(IOException::class)
    fun getMessages(baseUrl: String, token: String, contactId: String, limit: Int = 60): List<RemoteMessage> {
        val url = "${baseUrl.trimEnd('/')}/api/remote/v1/contacts/$contactId/messages?limit=$limit"
        val req = authorizedRequest(url, token).get().build()
        http.newCall(req).execute().use { resp ->
            val body = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) throw IOException(parseErrorText(resp.code, body))
            val parsed = messagesRespAdapter.fromJson(body)
            return parsed?.messages.orEmpty().map { it.toRemoteMessage() }
        }
    }

    @Throws(IOException::class)
    fun sendChat(baseUrl: String, token: String, message: String, contactId: String?): ChatOutcome {
        val url = "${baseUrl.trimEnd('/')}/api/remote/v1/chat"
        val json = chatReqAdapter.toJson(
            ChatRequest(message = message, contactId = contactId?.takeIf { it.isNotBlank() })
        )
        val req = authorizedRequest(url, token)
            .post(json.toRequestBody(jsonMediaType))
            .build()

        http.newCall(req).execute().use { resp ->
            val body = resp.body?.string().orEmpty()
            if (resp.isSuccessful) {
                val ok = chatRespAdapter.fromJson(body)
                return ChatOutcome(
                    ok = true,
                    source = "assistant",
                    text = ok?.reply ?: "(empty reply)",
                    conversationId = ok?.conversationId
                )
            }
            return ChatOutcome(ok = false, source = "error", text = parseErrorText(resp.code, body), conversationId = null)
        }
    }

    @Throws(IOException::class)
    fun sendChatAudio(baseUrl: String, token: String, audioFile: File, contactId: String?): ChatOutcome {
        val url = "${baseUrl.trimEnd('/')}/api/remote/v1/chat-audio"
        val formBody = MultipartBody.Builder()
            .setType(MultipartBody.FORM)
            .addFormDataPart("audio", audioFile.name, audioFile.asRequestBody("audio/3gpp".toMediaType()))
            .apply {
                val cid = contactId?.trim().orEmpty()
                if (cid.isNotEmpty()) addFormDataPart("contactId", cid)
            }
            .build()

        val req = authorizedRequest(url, token).post(formBody).build()
        http.newCall(req).execute().use { resp ->
            val body = resp.body?.string().orEmpty()
            if (resp.isSuccessful) {
                val ok = chatRespAdapter.fromJson(body)
                return ChatOutcome(
                    ok = true,
                    source = "assistant",
                    text = ok?.reply ?: "(empty reply)",
                    conversationId = ok?.conversationId
                )
            }
            return ChatOutcome(ok = false, source = "error", text = parseErrorText(resp.code, body), conversationId = null)
        }
    }

    private fun authorizedRequest(url: String, token: String): Request.Builder =
        Request.Builder().url(url).addHeader("Authorization", "Bearer $token")

    private fun parseErrorText(statusCode: Int, responseBody: String): String {
        val parsedError = errRespAdapter.fromJson(responseBody)?.error.orEmpty()
        val normalized = parsedError.lowercase()
        val message = when {
            statusCode == 401 || normalized == "unauthorized" -> "Unauthorized. Check your API token."
            normalized == "audio_field_required" -> "Audio upload failed: form field 'audio' is required."
            normalized == "multipart_form_required" -> "Audio upload failed: request must be multipart/form-data."
            normalized == "message_required" -> "Message is required."
            parsedError.isNotBlank() -> parsedError
            responseBody.isNotBlank() -> responseBody
            else -> "Request failed with HTTP $statusCode."
        }
        return "HTTP $statusCode: $message"
    }

    companion object {
        fun avatarUrl(baseUrl: String, contactId: String): String =
            "${baseUrl.trimEnd('/')}/api/remote/v1/contacts/$contactId/avatar"
    }
}

data class ChatOutcome(val ok: Boolean, val source: String, val text: String, val conversationId: String?)
data class ChatRequest(@Json(name = "message") val message: String, @Json(name = "contactId") val contactId: String? = null)
data class ChatResponse(@Json(name = "reply") val reply: String?, @Json(name = "conversationId") val conversationId: String?)
data class ErrorResponse(@Json(name = "error") val error: String?)
data class HealthResponse(@Json(name = "ok") val ok: Boolean?, @Json(name = "service") val service: String?)

data class ContactsResponse(@Json(name = "contacts") val contacts: List<AiContactDto>?)
data class AiContactDto(
    @Json(name = "id") val id: String?,
    @Json(name = "name") val name: String?,
    @Json(name = "description") val description: String?,
    @Json(name = "isPrimary") val isPrimary: Boolean?,
    @Json(name = "hasAvatar") val hasAvatar: Boolean?,
    @Json(name = "lastMessagePreview") val lastMessagePreview: String?,
    @Json(name = "lastMessageAt") val lastMessageAt: String?
) {
    fun toAiContact(): AiContact = AiContact(
        id = id.orEmpty(),
        name = name?.ifBlank { "AI Contact" } ?: "AI Contact",
        description = description,
        isPrimary = isPrimary == true,
        hasAvatar = hasAvatar == true,
        lastMessagePreview = lastMessagePreview,
        lastMessageAt = lastMessageAt
    )
}

data class MessagesResponse(
    @Json(name = "contactId") val contactId: String?,
    @Json(name = "messages") val messages: List<RemoteMessageDto>?
)

data class RemoteMessageDto(
    @Json(name = "id") val id: String?,
    @Json(name = "role") val role: String?,
    @Json(name = "content") val content: String?,
    @Json(name = "timestamp") val timestamp: String?
) {
    fun toRemoteMessage(): RemoteMessage = RemoteMessage(
        id = id.orEmpty(),
        role = when (role?.lowercase()) {
            "user" -> MessageRole.USER
            "assistant" -> MessageRole.ASSISTANT
            else -> MessageRole.SYSTEM
        },
        content = content.orEmpty(),
        timestampMs = parseTimestamp(timestamp)
    )

    private fun parseTimestamp(raw: String?): Long {
        if (raw.isNullOrBlank()) return System.currentTimeMillis()
        return runCatching { java.time.Instant.parse(raw).toEpochMilli() }.getOrElse {
            runCatching {
                val fmt = java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss", java.util.Locale.US)
                fmt.parse(raw.take(19))?.time ?: System.currentTimeMillis()
            }.getOrDefault(System.currentTimeMillis())
        }
    }
}
