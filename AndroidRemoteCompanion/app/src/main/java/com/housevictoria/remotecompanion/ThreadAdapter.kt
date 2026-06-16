package com.housevictoria.remotecompanion

import android.graphics.drawable.GradientDrawable
import android.view.Gravity
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.FrameLayout
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.TextView
import androidx.core.content.ContextCompat
import androidx.core.view.isVisible
import androidx.recyclerview.widget.DiffUtil
import androidx.recyclerview.widget.ListAdapter
import androidx.recyclerview.widget.RecyclerView

class ThreadAdapter(
    private val config: CompanionConfig,
    private val onClick: (AiContact) -> Unit
) : ListAdapter<AiContact, ThreadAdapter.Holder>(Diff) {

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): Holder {
        val view = LayoutInflater.from(parent.context).inflate(R.layout.item_thread, parent, false)
        return Holder(view)
    }

    override fun onBindViewHolder(holder: Holder, position: Int) {
        holder.bind(getItem(position), config, onClick)
    }

    class Holder(itemView: View) : RecyclerView.ViewHolder(itemView) {
        private val accent: View = itemView.findViewById(R.id.threadAccentStripe)
        private val ring: View = itemView.findViewById(R.id.threadAvatarRing)
        private val avatar: ImageView = itemView.findViewById(R.id.threadAvatar)
        private val name: TextView = itemView.findViewById(R.id.threadName)
        private val preview: TextView = itemView.findViewById(R.id.threadPreview)
        private val time: TextView = itemView.findViewById(R.id.threadTime)

        fun bind(contact: AiContact, config: CompanionConfig, onClick: (AiContact) -> Unit) {
            val theme = contact.theme()
            name.text = contact.name
            name.setTextColor(theme.accentBright)
            preview.text = contact.lastMessagePreview?.ifBlank { "Start a conversation…" } ?: "Start a conversation…"
            time.text = contact.relativeTimeLabel()

            accent.setBackgroundColor(theme.accent)
            (ring.background as? GradientDrawable)?.setStroke(
                (2f * itemView.resources.displayMetrics.density).toInt(),
                theme.avatarRing
            )

            AvatarLoader.load(itemView.context, avatar, config, contact.id, contact.hasAvatar, theme)
            itemView.setOnClickListener { onClick(contact) }
        }
    }

    private object Diff : DiffUtil.ItemCallback<AiContact>() {
        override fun areItemsTheSame(oldItem: AiContact, newItem: AiContact) = oldItem.id == newItem.id
        override fun areContentsTheSame(oldItem: AiContact, newItem: AiContact) = oldItem == newItem
    }
}
