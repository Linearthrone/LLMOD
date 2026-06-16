package com.housevictoria.remotecompanion

import android.graphics.drawable.GradientDrawable
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.ImageView
import android.widget.TextView
import androidx.core.view.isVisible
import androidx.recyclerview.widget.DiffUtil
import androidx.recyclerview.widget.ListAdapter
import androidx.recyclerview.widget.RecyclerView
import com.google.android.material.card.MaterialCardView
import com.google.android.material.chip.Chip

class ContactBookAdapter(
    private val config: CompanionConfig,
    private val onClick: (AiContact) -> Unit
) : ListAdapter<AiContact, ContactBookAdapter.Holder>(Diff) {

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): Holder {
        val view = LayoutInflater.from(parent.context).inflate(R.layout.item_contact_book, parent, false)
        return Holder(view)
    }

    override fun onBindViewHolder(holder: Holder, position: Int) {
        holder.bind(getItem(position), config, onClick)
    }

    class Holder(itemView: View) : RecyclerView.ViewHolder(itemView) {
        private val card: MaterialCardView = itemView.findViewById(R.id.contactCard)
        private val glow: View = itemView.findViewById(R.id.contactGlow)
        private val avatar: ImageView = itemView.findViewById(R.id.contactAvatar)
        private val name: TextView = itemView.findViewById(R.id.contactName)
        private val description: TextView = itemView.findViewById(R.id.contactDescription)
        private val primaryChip: Chip = itemView.findViewById(R.id.contactPrimaryChip)

        fun bind(contact: AiContact, config: CompanionConfig, onClick: (AiContact) -> Unit) {
            val theme = contact.theme()
            name.text = contact.name
            name.setTextColor(theme.accentBright)
            description.text = contact.description?.ifBlank { "AI persona" } ?: "AI persona"
            primaryChip.isVisible = contact.isPrimary
            primaryChip.setTextColor(theme.accentBright)

            glow.setBackgroundColor(theme.accentGlow)
            card.strokeColor = theme.accent
            card.strokeWidth = (1.2f * itemView.resources.displayMetrics.density).toInt()

            AvatarLoader.load(itemView.context, avatar, config, contact.id, contact.hasAvatar, theme)
            itemView.setOnClickListener { onClick(contact) }
        }
    }

    private object Diff : DiffUtil.ItemCallback<AiContact>() {
        override fun areItemsTheSame(oldItem: AiContact, newItem: AiContact) = oldItem.id == newItem.id
        override fun areContentsTheSame(oldItem: AiContact, newItem: AiContact) = oldItem == newItem
    }
}
