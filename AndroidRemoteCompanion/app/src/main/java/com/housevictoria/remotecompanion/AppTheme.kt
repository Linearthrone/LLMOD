package com.housevictoria.remotecompanion

import android.content.Context
import android.content.res.ColorStateList
import android.graphics.drawable.GradientDrawable
import android.widget.AutoCompleteTextView
import android.widget.TextView
import androidx.annotation.ColorInt
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import com.google.android.material.button.MaterialButton
import com.google.android.material.card.MaterialCardView
import com.google.android.material.textfield.TextInputEditText
import com.google.android.material.textfield.TextInputLayout

data class ThemePalette(
    @ColorInt val background: Int,
    @ColorInt val surface: Int,
    @ColorInt val surfaceElevated: Int,
    @ColorInt val primary: Int,
    @ColorInt val primaryDim: Int,
    @ColorInt val accent: Int,
    @ColorInt val textPrimary: Int,
    @ColorInt val textSecondary: Int,
    @ColorInt val textMuted: Int,
    @ColorInt val success: Int,
    @ColorInt val error: Int,
    @ColorInt val warning: Int,
    @ColorInt val bubbleStroke: Int
)

enum class AppTheme(val id: String, val displayName: String) {
    HOUSE_VICTORIA("house_victoria", "House Victoria Dark"),
    AMETHYST("amethyst", "Amethyst Night"),
    EMERALD("emerald", "Emerald Depth"),
    SUNSET("sunset", "Sunset Ember");

    fun palette(context: Context): ThemePalette = when (this) {
        HOUSE_VICTORIA -> ThemePalette(
            background = color(context, R.color.hv_background),
            surface = color(context, R.color.hv_surface),
            surfaceElevated = color(context, R.color.hv_surface_elevated),
            primary = color(context, R.color.hv_primary),
            primaryDim = color(context, R.color.hv_primary_dim),
            accent = color(context, R.color.hv_accent),
            textPrimary = color(context, R.color.hv_text_primary),
            textSecondary = color(context, R.color.hv_text_secondary),
            textMuted = color(context, R.color.hv_text_muted),
            success = color(context, R.color.hv_success),
            error = color(context, R.color.hv_error),
            warning = color(context, R.color.hv_warning),
            bubbleStroke = color(context, R.color.hv_bubble_stroke)
        )
        AMETHYST -> ThemePalette(
            background = 0xFF0A0614.toInt(),
            surface = 0xFF17102A.toInt(),
            surfaceElevated = 0xFF231836.toInt(),
            primary = 0xFFB794F6.toInt(),
            primaryDim = 0xFF8B5CF6.toInt(),
            accent = 0xFFF472B6.toInt(),
            textPrimary = 0xFFF3E8FF.toInt(),
            textSecondary = 0xFFC4B5FD.toInt(),
            textMuted = 0xFF7C6A9A.toInt(),
            success = 0xFF34D399.toInt(),
            error = 0xFFFF6B6B.toInt(),
            warning = 0xFFFBBF24.toInt(),
            bubbleStroke = 0x33B794F6.toInt()
        )
        EMERALD -> ThemePalette(
            background = 0xFF04120E.toInt(),
            surface = 0xFF0F241C.toInt(),
            surfaceElevated = 0xFF163528.toInt(),
            primary = 0xFF34D399.toInt(),
            primaryDim = 0xFF14B8A6.toInt(),
            accent = 0xFF60A5FA.toInt(),
            textPrimary = 0xFFE6FFF5.toInt(),
            textSecondary = 0xFF8FD9B8.toInt(),
            textMuted = 0xFF5A8A74.toInt(),
            success = 0xFF34D399.toInt(),
            error = 0xFFFF6B6B.toInt(),
            warning = 0xFFFBBF24.toInt(),
            bubbleStroke = 0x3334D399.toInt()
        )
        SUNSET -> ThemePalette(
            background = 0xFF140A08.toInt(),
            surface = 0xFF261512.toInt(),
            surfaceElevated = 0xFF35201B.toInt(),
            primary = 0xFFFF8A5B.toInt(),
            primaryDim = 0xFFE85D4C.toInt(),
            accent = 0xFFFBBF24.toInt(),
            textPrimary = 0xFFFFF1E8.toInt(),
            textSecondary = 0xFFE8B4A0.toInt(),
            textMuted = 0xFF9A6B5C.toInt(),
            success = 0xFF34D399.toInt(),
            error = 0xFFFF6B6B.toInt(),
            warning = 0xFFFBBF24.toInt(),
            bubbleStroke = 0x33FF8A5B.toInt()
        )
    }

    companion object {
        fun fromId(id: String?): AppTheme =
            entries.firstOrNull { it.id == id } ?: HOUSE_VICTORIA

        private fun color(context: Context, resId: Int): Int =
            ContextCompat.getColor(context, resId)
    }
}

object ThemeManager {
    fun current(context: Context): AppTheme = AppTheme.fromId(CompanionPrefs.loadThemeId(context))

    fun currentPalette(context: Context): ThemePalette = current(context).palette(context)

    fun applyToActivity(activity: AppCompatActivity) {
        val palette = currentPalette(activity)
        activity.window.statusBarColor = palette.background
        activity.window.navigationBarColor = palette.background
    }

    fun applyCard(card: MaterialCardView, palette: ThemePalette, widthDp: Float = 1f) {
        strokeCard(card, palette, widthDp)
    }

    fun strokeCard(card: MaterialCardView, palette: ThemePalette, widthDp: Float = 1f) {
        val density = card.resources.displayMetrics.density
        card.strokeColor = palette.bubbleStroke
        card.setCardBackgroundColor(palette.surface)
        card.strokeWidth = (widthDp * density).toInt().coerceAtLeast(1)
    }

    fun applyInputLayout(layout: TextInputLayout, palette: ThemePalette) {
        layout.boxBackgroundColor = palette.surface
        layout.setBoxStrokeColorStateList(ColorStateList.valueOf(palette.bubbleStroke))
        layout.setHintTextColor(ColorStateList.valueOf(palette.textMuted))
        layout.defaultHintTextColor = ColorStateList.valueOf(palette.textMuted)
        when (val edit = layout.editText) {
            is TextInputEditText -> edit.setTextColor(palette.textPrimary)
            is AutoCompleteTextView -> edit.setTextColor(palette.textPrimary)
        }
    }

    fun applyText(textView: TextView, palette: ThemePalette, secondary: Boolean = false, muted: Boolean = false) {
        textView.setTextColor(
            when {
                muted -> palette.textMuted
                secondary -> palette.textSecondary
                else -> palette.textPrimary
            }
        )
    }

    fun applyFilledButton(button: MaterialButton, palette: ThemePalette) {
        button.backgroundTintList = ColorStateList.valueOf(palette.primary)
        button.setTextColor(palette.background)
        button.iconTint = ColorStateList.valueOf(palette.background)
    }

    fun applyOutlinedButton(button: MaterialButton, palette: ThemePalette) {
        button.strokeColor = ColorStateList.valueOf(palette.primary)
        button.setTextColor(palette.primary)
        button.iconTint = ColorStateList.valueOf(palette.primary)
    }

    fun meshBackground(palette: ThemePalette): GradientDrawable =
        GradientDrawable(
            GradientDrawable.Orientation.TL_BR,
            intArrayOf(palette.background, palette.surface, palette.background)
        )
}
