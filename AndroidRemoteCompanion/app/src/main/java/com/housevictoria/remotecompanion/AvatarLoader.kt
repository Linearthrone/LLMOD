package com.housevictoria.remotecompanion

import android.content.Context
import android.widget.ImageView
import coil.ImageLoader
import coil.request.ImageRequest
import coil.transform.CircleCropTransformation

object AvatarLoader {
    private var cachedLoader: ImageLoader? = null

    fun load(
        context: Context,
        imageView: ImageView,
        config: CompanionConfig,
        contactId: String,
        hasAvatar: Boolean,
        theme: ContactTheme,
        placeholderRes: Int = R.drawable.ic_sparkle
    ) {
        imageView.setImageResource(placeholderRes)
        imageView.setColorFilter(theme.accentBright)
        if (!hasAvatar || config.baseUrl.isBlank() || config.token.length < 16) return

        val loader = cachedLoader ?: ImageLoader.Builder(context.applicationContext).build().also {
            cachedLoader = it
        }

        val request = ImageRequest.Builder(context)
            .data(RemoteApiClient.avatarUrl(config.baseUrl, contactId))
            .addHeader("Authorization", "Bearer ${config.token}")
            .crossfade(320)
            .transformations(CircleCropTransformation())
            .target(
                onSuccess = { drawable ->
                    imageView.setColorFilter(null)
                    imageView.setImageDrawable(drawable)
                },
                onError = {
                    imageView.setImageResource(placeholderRes)
                    imageView.setColorFilter(theme.accentBright)
                }
            )
            .build()

        loader.enqueue(request)
    }
}
