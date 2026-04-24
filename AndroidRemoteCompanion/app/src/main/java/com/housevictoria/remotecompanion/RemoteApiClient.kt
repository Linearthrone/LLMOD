package com.housevictoria.remotecompanion

import com.squareup.moshi.Json
import com.squareup.moshi.Moshi
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.asRequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.IOException
import java.io.File

class RemoteApiClient {
    private val http = OkHttpClient()
    private val moshi = Moshi.Builder().build()
    private val chatReqAdapter = moshi.adapter(ChatRequest::class.java)
    private val chatRespAdapter = moshi.adapter(ChatResponse::class.java)
    private val healthRespAdapter = moshi.adapter(HealthResponse::class.java)
    private val errRespAdapter = moshi.adapter(ErrorResponse::class.java)
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
    fun sendChat(baseUrl: String, token: String, message: String, contactId: String?): ChatOutcome {
        val url = "${baseUrl.trimEnd('/')}/api/remote/v1/chat"
        val json = chatReqAdapter.toJson(ChatRequest(message = message, contactId = contactId?.takeIf { it.isNotBlank() }))
        val req = Request.Builder()
            .url(url)
            .addHeader("Authorization", "Bearer $token")
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
            val err = parseErrorText(resp.code, body)
            return ChatOutcome(ok = false, source = "error", text = err, conversationId = null)
        }
    }

    @Throws(IOException::class)
    fun sendChatAudio(baseUrl: String, token: String, audioFile: File, contactId: String?): ChatOutcome {
        val url = "${baseUrl.trimEnd('/')}/api/remote/v1/chat-audio"
        val formBody = MultipartBody.Builder()
            .setType(MultipartBody.FORM)
            .addFormDataPart(
                "audio",
                audioFile.name,
                audioFile.asRequestBody("audio/3gpp".toMediaType())
            )
            .apply {
                val cid = contactId?.trim().orEmpty()
                if (cid.isNotEmpty()) {
                    addFormDataPart("contactId", cid)
                }
            }
            .build()

        val req = Request.Builder()
            .url(url)
            .addHeader("Authorization", "Bearer $token")
            .post(formBody)
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
            val err = parseErrorText(resp.code, body)
            return ChatOutcome(ok = false, source = "error", text = err, conversationId = null)
        }
    }

    private fun parseErrorText(statusCode: Int, responseBody: String): String {
        val parsedError = errRespAdapter.fromJson(responseBody)?.error.orEmpty()
        val normalized = parsedError.lowercase()
        val message = when {
            statusCode == 401 || normalized == "unauthorized" ->
                "Unauthorized. Check your API token."
            normalized == "audio_field_required" ->
                "Audio upload failed: form field 'audio' is required."
            normalized == "multipart_form_required" ->
                "Audio upload failed: request must be multipart/form-data."
            normalized == "message_required" ->
                "Message is required."
            parsedError.isNotBlank() ->
                parsedError
            responseBody.isNotBlank() ->
                responseBody
            else ->
                "Request failed with HTTP $statusCode."
        }
        return "HTTP $statusCode: $message"
    }
}

data class ChatOutcome(val ok: Boolean, val source: String, val text: String, val conversationId: String?)
data class ChatRequest(@Json(name = "message") val message: String, @Json(name = "contactId") val contactId: String? = null)
data class ChatResponse(@Json(name = "reply") val reply: String?, @Json(name = "conversationId") val conversationId: String?)
data class ErrorResponse(@Json(name = "error") val error: String?)
data class HealthResponse(@Json(name = "ok") val ok: Boolean?, @Json(name = "service") val service: String?)
