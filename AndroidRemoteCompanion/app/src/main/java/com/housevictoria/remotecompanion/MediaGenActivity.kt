package com.housevictoria.remotecompanion

import android.graphics.BitmapFactory
import android.os.Bundle
import android.view.View
import android.widget.ArrayAdapter
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.isVisible
import androidx.core.view.updatePadding
import androidx.recyclerview.widget.GridLayoutManager
import androidx.recyclerview.widget.RecyclerView
import coil.load
import com.housevictoria.remotecompanion.databinding.ActivityGalleryBinding
import com.housevictoria.remotecompanion.databinding.ActivityMediaGenBinding
import kotlin.math.max
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File

class MediaGenActivity : AppCompatActivity() {
    private lateinit var binding: ActivityMediaGenBinding
    private val apiClient = RemoteApiClient()
    private val uiScope = CoroutineScope(Job() + Dispatchers.Main)
    private var config = CompanionConfig("", "")
    private var imageModels = listOf<MediaModelDto>()
    private var videoModels = listOf<MediaModelDto>()

    override fun onCreate(savedInstanceState: Bundle?) {
        ThemeManager.applyToActivity(this)
        super.onCreate(savedInstanceState)
        WindowCompat.setDecorFitsSystemWindows(window, false)
        binding = ActivityMediaGenBinding.inflate(layoutInflater)
        setContentView(binding.root)
        applyThemeColors()
        setupWindowInsets()
        setupModelDropdown()
        setupInputFocusScroll()

        config = CompanionPrefs.load(this)
        setSupportActionBar(binding.mediaGenToolbar)
        BottomNavHelper.wire(this, binding.bottomNav, R.id.nav_media_gen)

        binding.mediaTypeImage.isChecked = true
        binding.generateButton.setOnClickListener { generate() }
        loadModels()
    }

    private fun setupWindowInsets() {
        ViewCompat.setOnApplyWindowInsetsListener(binding.mediaGenRoot) { _, insets ->
            val bars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            val ime = insets.getInsets(WindowInsetsCompat.Type.ime())
            val density = resources.displayMetrics.density
            val bottomNavHeight = (72 * density).toInt()
            val bottomInset = max(bars.bottom, ime.bottom)

            binding.mediaGenScroll.updatePadding(
                top = bars.top,
                bottom = bottomInset + bottomNavHeight + (20 * density).toInt()
            )

            insets
        }
    }

    private fun setupModelDropdown() {
        binding.mediaModelInput.threshold = 0
        binding.mediaModelInput.setOnClickListener { binding.mediaModelInput.showDropDown() }
    }

    private fun setupInputFocusScroll() {
        val scrollIntoView = { view: View ->
            binding.mediaGenScroll.post {
                binding.mediaGenScroll.smoothScrollTo(0, view.top)
            }
        }
        binding.positivePromptInput.setOnFocusChangeListener { v, hasFocus ->
            if (hasFocus) scrollIntoView(v)
        }
        binding.negativePromptInput.setOnFocusChangeListener { v, hasFocus ->
            if (hasFocus) scrollIntoView(v)
        }
        binding.mediaModelInput.setOnFocusChangeListener { v, hasFocus ->
            if (hasFocus) {
                binding.mediaModelInput.showDropDown()
                scrollIntoView(v)
            }
        }
    }

    override fun onDestroy() {
        uiScope.cancel()
        super.onDestroy()
    }

    private fun applyThemeColors() {
        val palette = ThemeManager.currentPalette(this)
        binding.mediaGenRoot.background = ThemeManager.meshBackground(palette)
        binding.mediaGenToolbar.setTitleTextColor(palette.textPrimary)
        binding.mediaGenToolbar.setSubtitleTextColor(palette.textSecondary)

        listOf(
            binding.mediaModelInput.parent as? com.google.android.material.textfield.TextInputLayout,
            binding.positivePromptInput.parent as? com.google.android.material.textfield.TextInputLayout,
            binding.negativePromptInput.parent as? com.google.android.material.textfield.TextInputLayout
        ).forEach { layout ->
            layout?.let { ThemeManager.applyInputLayout(it, palette) }
        }

        ThemeManager.applyOutlinedButton(binding.mediaTypeImage, palette)
        ThemeManager.applyOutlinedButton(binding.mediaTypeVideo, palette)
        ThemeManager.applyFilledButton(binding.generateButton, palette)
        ThemeManager.applyText(binding.mediaGenStatus, palette, secondary = true)
        binding.previewImage.setBackgroundColor(palette.surfaceElevated)
    }

    private fun loadModels() {
        if (config.validate() != null) return
        uiScope.launch {
            val result = withContext(Dispatchers.IO) {
                runCatching { apiClient.getMediaModels(config.baseUrl, config.token) }
            }
            result.onSuccess { models ->
                imageModels = models.imageModels.orEmpty()
                videoModels = models.videoModels.orEmpty()
                updateModelDropdown(isVideo = false)
            }
        }
        binding.mediaTypeToggle.addOnButtonCheckedListener { _, checkedId, isChecked ->
            if (!isChecked) return@addOnButtonCheckedListener
            updateModelDropdown(checkedId == R.id.mediaTypeVideo)
        }
    }

    private fun updateModelDropdown(isVideo: Boolean) {
        val models = if (isVideo) videoModels else imageModels
        val labels = models.map { it.label ?: it.id.orEmpty() }
        val adapter = ArrayAdapter(this, android.R.layout.simple_dropdown_item_1line, labels)
        binding.mediaModelInput.setAdapter(adapter)
        if (labels.isNotEmpty()) binding.mediaModelInput.setText(labels.first(), false)
    }

    private fun generate() {
        val error = config.validate()
        if (error != null) {
            toast(error)
            return
        }
        val positive = binding.positivePromptInput.text?.toString().orEmpty().trim()
        if (positive.isBlank()) {
            toast(getString(R.string.hint_positive_prompt))
            return
        }
        val negative = binding.negativePromptInput.text?.toString()?.trim()
        val isVideo = binding.mediaTypeVideo.isChecked
        val mediaType = if (isVideo) "video" else "image"
        val modelLabel = binding.mediaModelInput.text?.toString().orEmpty()
        val models = if (isVideo) videoModels else imageModels
        val modelId = models.firstOrNull { it.label == modelLabel }?.id ?: models.firstOrNull()?.id

        binding.generateButton.isEnabled = false
        binding.mediaGenStatus.text = getString(R.string.generating)

        uiScope.launch {
            val result = withContext(Dispatchers.IO) {
                runCatching {
                    apiClient.generateMedia(config.baseUrl, config.token, mediaType, positive, negative, modelId)
                }
            }
            binding.generateButton.isEnabled = true
            result.onSuccess { response ->
                if (response.ok != true || response.asset?.id.isNullOrBlank()) {
                    binding.mediaGenStatus.text = response.error ?: getString(R.string.generation_failed)
                    return@onSuccess
                }
                val assetId = response.asset!!.id!!
                val bytes = withContext(Dispatchers.IO) {
                    apiClient.downloadMedia(config.baseUrl, config.token, assetId)
                }
                val item = GalleryStore.saveBytes(
                    this@MediaGenActivity,
                    bytes,
                    response.asset.mediaType ?: "image",
                    positive
                )
                binding.mediaGenStatus.text = getString(R.string.generation_saved)
                if (!item.isVideo()) {
                    binding.previewImage.isVisible = true
                    binding.previewImage.setImageBitmap(BitmapFactory.decodeByteArray(bytes, 0, bytes.size))
                } else {
                    binding.previewImage.isVisible = false
                }
            }.onFailure {
                binding.mediaGenStatus.text = it.message ?: getString(R.string.generation_failed)
            }
        }
    }

    private fun toast(text: String) = Toast.makeText(this, text, Toast.LENGTH_SHORT).show()
}

class GalleryActivity : AppCompatActivity() {
    private lateinit var binding: ActivityGalleryBinding

    override fun onCreate(savedInstanceState: Bundle?) {
        ThemeManager.applyToActivity(this)
        super.onCreate(savedInstanceState)
        binding = ActivityGalleryBinding.inflate(layoutInflater)
        setContentView(binding.root)

        val palette = ThemeManager.currentPalette(this)
        binding.galleryRoot.background = ThemeManager.meshBackground(palette)
        binding.galleryToolbar.setTitleTextColor(palette.textPrimary)
        binding.galleryToolbar.setSubtitleTextColor(palette.textSecondary)

        setSupportActionBar(binding.galleryToolbar)
        BottomNavHelper.wire(this, binding.bottomNav, R.id.nav_gallery)

        binding.galleryRecycler.layoutManager = GridLayoutManager(this, 2)
        refreshGallery()
    }

    override fun onResume() {
        super.onResume()
        refreshGallery()
    }

    private fun refreshGallery() {
        val items = GalleryStore.list(this)
        binding.galleryEmpty.isVisible = items.isEmpty()
        binding.galleryRecycler.isVisible = items.isNotEmpty()
        binding.galleryRecycler.adapter = GalleryAdapter(items)
    }
}

private class GalleryAdapter(private val items: List<GalleryItem>) :
    RecyclerView.Adapter<GalleryAdapter.Holder>() {

    override fun onCreateViewHolder(parent: android.view.ViewGroup, viewType: Int): Holder {
        val view = android.view.LayoutInflater.from(parent.context)
            .inflate(R.layout.item_gallery, parent, false)
        return Holder(view)
    }

    override fun getItemCount(): Int = items.size

    override fun onBindViewHolder(holder: Holder, position: Int) {
        holder.bind(items[position])
    }

    class Holder(itemView: View) : RecyclerView.ViewHolder(itemView) {
        private val thumb = itemView.findViewById<android.widget.ImageView>(R.id.galleryThumb)
        fun bind(item: GalleryItem) {
            thumb.load(File(item.localPath)) {
                crossfade(true)
            }
        }
    }
}
