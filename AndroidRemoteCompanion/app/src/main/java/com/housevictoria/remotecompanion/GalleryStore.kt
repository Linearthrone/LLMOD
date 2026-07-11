package com.housevictoria.remotecompanion

import android.content.Context
import android.graphics.BitmapFactory
import android.net.Uri
import android.os.Environment
import android.provider.MediaStore
import android.webkit.MimeTypeMap
import java.io.File
import java.io.FileOutputStream
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.UUID

data class GalleryItem(
    val id: String,
    val fileName: String,
    val mediaType: String,
    val createdAt: Long,
    val positivePrompt: String?,
    val localPath: String
) {
    fun isVideo(): Boolean = mediaType == "video"
}

object GalleryStore {
    private const val PREFS = "remote_companion_gallery"
    private const val KEY_INDEX = "index_json"

    fun galleryDir(context: Context): File {
        val dir = File(context.filesDir, "gallery")
        if (!dir.exists()) dir.mkdirs()
        return dir
    }

    fun list(context: Context): List<GalleryItem> {
        val dir = galleryDir(context)
        return dir.listFiles()
            ?.filter { it.isFile }
            ?.sortedByDescending { it.lastModified() }
            ?.map { file ->
                GalleryItem(
                    id = file.nameWithoutExtension,
                    fileName = file.name,
                    mediaType = if (file.extension.lowercase() in setOf("mp4", "webm", "mov")) "video" else "image",
                    createdAt = file.lastModified(),
                    positivePrompt = null,
                    localPath = file.absolutePath
                )
            }.orEmpty()
    }

    fun saveBytes(
        context: Context,
        bytes: ByteArray,
        mediaType: String,
        positivePrompt: String? = null,
        extension: String = if (mediaType == "video") "mp4" else "jpg"
    ): GalleryItem {
        val id = UUID.randomUUID().toString().replace("-", "")
        val fileName = "$id.$extension"
        val file = File(galleryDir(context), fileName)
        FileOutputStream(file).use { it.write(bytes) }

        val item = GalleryItem(
            id = id,
            fileName = fileName,
            mediaType = mediaType,
            createdAt = System.currentTimeMillis(),
            positivePrompt = positivePrompt,
            localPath = file.absolutePath
        )

        exportToDeviceGallery(context, file, mediaType)
        return item
    }

    private fun exportToDeviceGallery(context: Context, file: File, mediaType: String) {
        runCatching {
            val mime = when {
                mediaType == "video" -> "video/mp4"
                file.extension.equals("png", true) -> "image/png"
                else -> "image/jpeg"
            }
            val collection = if (mediaType == "video") {
                MediaStore.Video.Media.EXTERNAL_CONTENT_URI
            } else {
                MediaStore.Images.Media.EXTERNAL_CONTENT_URI
            }
            val values = android.content.ContentValues().apply {
                put(MediaStore.MediaColumns.DISPLAY_NAME, file.name)
                put(MediaStore.MediaColumns.MIME_TYPE, mime)
                put(MediaStore.MediaColumns.RELATIVE_PATH, Environment.DIRECTORY_PICTURES + "/VictoriaLink")
            }
            val resolver = context.contentResolver
            val uri = resolver.insert(collection, values) ?: return
            resolver.openOutputStream(uri)?.use { out ->
                file.inputStream().use { it.copyTo(out) }
            }
        }
    }

    fun uriForItem(item: GalleryItem): Uri = Uri.fromFile(File(item.localPath))
}
