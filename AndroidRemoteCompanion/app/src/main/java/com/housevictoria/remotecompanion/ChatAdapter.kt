package com.housevictoria.remotecompanion

import android.animation.ObjectAnimator
import android.view.Gravity
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.view.animation.AccelerateDecelerateInterpolator
import android.widget.FrameLayout
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.TextView
import androidx.core.content.ContextCompat
import androidx.core.view.isVisible
import androidx.recyclerview.widget.DiffUtil
import coil.load
import coil.request.ImageRequest
import androidx.recyclerview.widget.ListAdapter
import androidx.recyclerview.widget.RecyclerView

class ChatAdapter(
    private var theme: ContactTheme = ContactThemePalette.forContact("default"),
    private var assistantName: String = "Victoria",
    private var config: CompanionConfig = CompanionConfig("", "")
) : ListAdapter<ChatMessage, RecyclerView.ViewHolder>(Diff) {

    fun setConfig(value: CompanionConfig) {
        config = value
    }

    fun setTheme(value: ContactTheme) {
        theme = value
        notifyDataSetChanged()
    }

    fun setAssistantName(name: String) {
        assistantName = name
    }

    override fun getItemViewType(position: Int): Int = when (getItem(position).role) {
        MessageRole.TYPING -> VIEW_TYPING
        MessageRole.SYSTEM -> VIEW_SYSTEM
        else -> VIEW_MESSAGE
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): RecyclerView.ViewHolder {
        val inflater = LayoutInflater.from(parent.context)
        return when (viewType) {
            VIEW_TYPING -> TypingViewHolder(inflater.inflate(R.layout.item_typing_indicator, parent, false), theme)
            else -> MessageViewHolder(inflater.inflate(R.layout.item_chat_message, parent, false))
        }
    }

    override fun onBindViewHolder(holder: RecyclerView.ViewHolder, position: Int) {
        when (holder) {
            is MessageViewHolder -> holder.bind(getItem(position), theme, assistantName, config)
            is TypingViewHolder -> holder.bind()
        }
    }

    class MessageViewHolder(itemView: View) : RecyclerView.ViewHolder(itemView) {
        private val messageRow: LinearLayout = itemView.findViewById(R.id.messageRow)
        private val systemMessage: TextView = itemView.findViewById(R.id.systemMessage)
        private val avatarContainer: FrameLayout = itemView.findViewById(R.id.avatarContainer)
        private val avatarIcon: ImageView = itemView.findViewById(R.id.avatarIcon)
        private val senderLabel: TextView = itemView.findViewById(R.id.senderLabel)
        private val messageBody: TextView = itemView.findViewById(R.id.messageBody)
        private val messageImage: ImageView = itemView.findViewById(R.id.messageImage)
        private val timestampText: TextView = itemView.findViewById(R.id.timestampText)

        fun bind(message: ChatMessage, theme: ContactTheme, assistantName: String, config: CompanionConfig) {
            if (message.role == MessageRole.SYSTEM) {
                messageRow.isVisible = false
                systemMessage.isVisible = true
                systemMessage.text = message.content
                return
            }

            messageRow.isVisible = true
            systemMessage.isVisible = false

            val isUser = message.role == MessageRole.USER
            val isError = message.role == MessageRole.ERROR

            messageRow.gravity = if (isUser) Gravity.END else Gravity.START
            messageRow.layoutDirection = if (isUser) View.LAYOUT_DIRECTION_RTL else View.LAYOUT_DIRECTION_LTR

            avatarContainer.isVisible = !isError
            avatarIcon.setImageResource(if (isUser) R.drawable.ic_person else R.drawable.ic_sparkle)
            avatarIcon.setColorFilter(if (isUser) theme.accent else theme.accentBright)

            senderLabel.isVisible = !isError
            senderLabel.text = if (isUser) {
                itemView.context.getString(R.string.label_you)
            } else {
                assistantName
            }
            senderLabel.setTextColor(theme.accent)

            messageBody.background = when {
                isUser -> theme.userBubbleDrawable(itemView.context)
                isError -> ContextCompat.getDrawable(itemView.context, R.drawable.bg_bubble_error)
                else -> theme.assistantBubbleDrawable(itemView.context)
            }
            messageBody.text = message.content
            messageBody.setTextColor(
                ContextCompat.getColor(
                    itemView.context,
                    if (isError) R.color.hv_error else R.color.hv_text_primary
                )
            )
            messageBody.isVisible = message.content.isNotBlank()

            if (message.hasMedia && config.validate() == null) {
                messageImage.isVisible = true
                val url = RemoteApiClient.messageMediaUrl(config.baseUrl, message.id)
                messageImage.load(url) {
                    crossfade(true)
                    addHeader("Authorization", "Bearer ${config.token}")
                }
            } else {
                messageImage.isVisible = false
            }

            timestampText.isVisible = message.role == MessageRole.ASSISTANT || isUser
            timestampText.text = message.formattedTime()
        }
    }

    class TypingViewHolder(itemView: View, private val theme: ContactTheme) : RecyclerView.ViewHolder(itemView) {
        private val dot1: View = itemView.findViewById(R.id.typingDot1)
        private val dot2: View = itemView.findViewById(R.id.typingDot2)
        private val dot3: View = itemView.findViewById(R.id.typingDot3)
        private var started = false

        init {
            dot1.setBackgroundColor(theme.accent)
            dot2.setBackgroundColor(theme.accentBright)
            dot3.setBackgroundColor(theme.accent)
        }

        fun bind() {
            if (started) return
            started = true
            animateDot(dot1, 0L)
            animateDot(dot2, 150L)
            animateDot(dot3, 300L)
        }

        private fun animateDot(dot: View, delay: Long) {
            ObjectAnimator.ofFloat(dot, View.ALPHA, 0.35f, 1f).apply {
                duration = 500
                startDelay = delay
                repeatCount = ObjectAnimator.INFINITE
                repeatMode = ObjectAnimator.REVERSE
                interpolator = AccelerateDecelerateInterpolator()
                start()
            }
        }
    }

    private object Diff : DiffUtil.ItemCallback<ChatMessage>() {
        override fun areItemsTheSame(oldItem: ChatMessage, newItem: ChatMessage) = oldItem.id == newItem.id
        override fun areContentsTheSame(oldItem: ChatMessage, newItem: ChatMessage) = oldItem == newItem
    }

    companion object {
        private const val VIEW_MESSAGE = 0
        private const val VIEW_TYPING = 1
        private const val VIEW_SYSTEM = 2
    }
}
