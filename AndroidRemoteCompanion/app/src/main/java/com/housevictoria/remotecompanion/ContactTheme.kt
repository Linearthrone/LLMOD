package com.housevictoria.remotecompanion

import android.content.Context
import android.graphics.Color
import android.graphics.drawable.GradientDrawable
import androidx.core.graphics.ColorUtils
import kotlin.math.abs

data class ContactTheme(
    val accent: Int,
    val accentBright: Int,
    val accentGlow: Int,
    val bubbleAssistant: Int,
    val bubbleUser: Int,
    val bubbleStroke: Int,
    val avatarRing: Int
) {
    fun assistantBubbleDrawable(context: Context): GradientDrawable =
        bubbleDrawable(context, bubbleAssistant, bubbleStroke)

    fun userBubbleDrawable(context: Context): GradientDrawable =
        bubbleDrawable(context, bubbleUser, ColorUtils.setAlphaComponent(accent, 90))

    private fun bubbleDrawable(context: Context, fill: Int, stroke: Int): GradientDrawable =
        GradientDrawable().apply {
            shape = GradientDrawable.RECTANGLE
            cornerRadii = floatArrayOf(20f, 20f, 20f, 20f, 20f, 20f, 6f, 6f)
            setColor(fill)
            setStroke((1.2f * context.resources.displayMetrics.density).toInt(), stroke)
        }
}

object ContactThemePalette {
    private val seeds = intArrayOf(
        Color.parseColor("#00D4FF"),
        Color.parseColor("#8B5CF6"),
        Color.parseColor("#14B8A6"),
        Color.parseColor("#F472B6"),
        Color.parseColor("#FBBF24"),
        Color.parseColor("#60A5FA"),
        Color.parseColor("#A78BFA"),
        Color.parseColor("#34D399")
    )

    fun forContact(contactId: String): ContactTheme {
        val accent = seeds[abs(contactId.hashCode()) % seeds.size]
        val bright = ColorUtils.blendARGB(accent, Color.WHITE, 0.25f)
        val glow = ColorUtils.setAlphaComponent(accent, 48)
        val assistant = ColorUtils.blendARGB(Color.parseColor("#111D32"), accent, 0.18f)
        val user = ColorUtils.blendARGB(Color.parseColor("#1A2942"), accent, 0.32f)
        val stroke = ColorUtils.setAlphaComponent(accent, 110)
        val ring = ColorUtils.setAlphaComponent(accent, 180)
        return ContactTheme(accent, bright, glow, assistant, user, stroke, ring)
    }
}
