package com.housevictoria.remotecompanion

import android.graphics.drawable.GradientDrawable
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import android.widget.Toast
import androidx.fragment.app.Fragment
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView

class SettingsThemesFragment : Fragment() {
    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View = inflater.inflate(R.layout.page_settings_themes, container, false)

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        val palette = ThemeManager.currentPalette(requireContext())
        view.findViewById<TextView>(R.id.themesHeader)?.let { ThemeManager.applyText(it, palette) }
        view.findViewById<TextView>(R.id.themesSubtitle)?.let { ThemeManager.applyText(it, palette, muted = true) }

        val recycler = view.findViewById<RecyclerView>(R.id.themesRecycler)
        val selected = ThemeManager.current(requireContext())
        recycler.layoutManager = LinearLayoutManager(requireContext())
        recycler.adapter = ThemePickerAdapter(selected) { theme ->
            CompanionPrefs.saveThemeId(requireContext(), theme.id)
            Toast.makeText(requireContext(), R.string.theme_applied, Toast.LENGTH_SHORT).show()
            requireActivity().recreate()
        }
    }
}

private class ThemePickerAdapter(
    private val selected: AppTheme,
    private val onPick: (AppTheme) -> Unit
) : RecyclerView.Adapter<ThemePickerAdapter.Holder>() {

    private val items = AppTheme.entries.toList()

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): Holder {
        val view = LayoutInflater.from(parent.context).inflate(R.layout.item_theme, parent, false)
        return Holder(view)
    }

    override fun getItemCount(): Int = items.size

    override fun onBindViewHolder(holder: Holder, position: Int) {
        holder.bind(items[position], items[position] == selected, onPick)
    }

    class Holder(itemView: View) : RecyclerView.ViewHolder(itemView) {
        private val name: TextView = itemView.findViewById(R.id.themeName)
        private val swPrimary: View = itemView.findViewById(R.id.swPrimary)
        private val swAccent: View = itemView.findViewById(R.id.swAccent)
        private val swSurface: View = itemView.findViewById(R.id.swSurface)

        fun bind(theme: AppTheme, isSelected: Boolean, onPick: (AppTheme) -> Unit) {
            val palette = theme.palette(itemView.context)
            name.text = if (isSelected) "✓ ${theme.displayName}" else theme.displayName
            name.setTextColor(palette.textPrimary)
            swPrimary.background = rounded(palette.primary)
            swAccent.background = rounded(palette.accent)
            swSurface.background = rounded(palette.surface)
            itemView.setOnClickListener { onPick(theme) }
        }

        private fun rounded(color: Int): GradientDrawable =
            GradientDrawable().apply {
                cornerRadius = 12f
                setColor(color)
            }
    }
}
