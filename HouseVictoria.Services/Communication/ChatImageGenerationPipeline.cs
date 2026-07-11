using System.Collections.Concurrent;
using System.Net.Http;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Communication
{
    /// <summary>
    /// Shared image-generation path for chat flows (desktop SMS and remote companion).
    /// Detects user image intent via <see cref="FileDeliveryHelper"/>, calls <see cref="IAIService"/>,
    /// and saves PNG bytes for inline chat display.
    /// </summary>
    public sealed class ChatImageGenerationPipeline
    {
        private readonly IAIService _aiService;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ConcurrentDictionary<string, string> _lastPromptByConversation = new(StringComparer.Ordinal);

        public ChatImageGenerationPipeline(IAIService aiService)
        {
            _aiService = aiService;
        }

        public bool IsGenerationInProgress => _lock.CurrentCount == 0;

        public bool ShouldGenerateImageForMessage(string conversationId, string message)
        {
            var isFollowUp = IsImageFollowUpRequest(message)
                && _lastPromptByConversation.ContainsKey(conversationId);
            return FileDeliveryHelper.ShouldAttemptImageGeneration(message, isFollowUp);
        }

        public bool ShouldCatchUpImageGeneration(string conversationId, string userMessage, string aiResponse)
        {
            var isFollowUp = IsImageFollowUpRequest(userMessage)
                && _lastPromptByConversation.ContainsKey(conversationId);
            if (FileDeliveryHelper.ShouldAttemptImageGeneration(userMessage, isFollowUp))
                return true;

            return FileDeliveryHelper.AiPromisesOrClaimsImageDelivery(aiResponse);
        }

        public static bool IsImageStatusInquiry(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;
            var m = message.Trim();
            return m.Contains("where is", StringComparison.OrdinalIgnoreCase)
                || m.Contains("where's", StringComparison.OrdinalIgnoreCase)
                || m.Contains("wheres the", StringComparison.OrdinalIgnoreCase)
                || m.Contains("did you send", StringComparison.OrdinalIgnoreCase)
                || m.Contains("still working", StringComparison.OrdinalIgnoreCase)
                || m.Contains("is it ready", StringComparison.OrdinalIgnoreCase);
        }

        public static string? BuildImageChatGuardNote(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;
            var m = message.Trim();
            var mentionsImage = m.Contains("picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("photo", StringComparison.OrdinalIgnoreCase)
                || m.Contains("image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("selfie", StringComparison.OrdinalIgnoreCase);
            if (!mentionsImage)
                return null;
            return "[System: You cannot attach or transmit images in chat. Do not claim you sent, uploaded, or attached photos. Image delivery is handled separately by the app when the user explicitly requests generation.]";
        }

        public string GetStatusMessage(bool queued) =>
            queued
                ? "🎨 Queued — finishing your previous image, then starting this one…"
                : "🎨 Generating your image… this can take up to a minute.";

        public async Task<ChatImageGenerationResult> GenerateAsync(
            AIContact contact,
            string userMessage,
            string conversationId,
            CancellationToken cancellationToken = default)
        {
            var queued = _lock.CurrentCount == 0;
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await ProcessImageGenerationRequestAsync(contact, userMessage, conversationId)
                    .ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<ChatImageGenerationResult> ProcessImageGenerationRequestAsync(
            AIContact contact,
            string userMessage,
            string conversationId)
        {
            try
            {
                var userPrompt = ResolveImagePrompt(conversationId, userMessage);
                var detailedPrompt = await _aiService.EnhanceImagePromptAsync(contact, userPrompt).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(detailedPrompt))
                    detailedPrompt = userPrompt;

                await using var imageStream = await _aiService.GenerateImageAsync(contact, detailedPrompt).ConfigureAwait(false);
                using var ms = new MemoryStream();
                await imageStream.CopyToAsync(ms).ConfigureAwait(false);
                var imageBytes = ms.ToArray();
                if (imageBytes.Length == 0)
                {
                    return ChatImageGenerationResult.Failure(
                        "❌ Image generation returned an empty file. Check Settings → Image Generation (A2E token or ComfyUI).");
                }

                _lastPromptByConversation[conversationId] = detailedPrompt;

                var chatFilePath = await SaveImageToConversationMediaAsync(conversationId, imageBytes).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine(
                    $"ChatImageGenerationPipeline: saved {imageBytes.Length} bytes to {chatFilePath}");

                return ChatImageGenerationResult.Ok(
                    "Here's your image.",
                    imageBytes,
                    chatFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ChatImageGenerationPipeline failed: {ex.Message}\n{ex.StackTrace}");
                var msg = ex.Message;
                if (msg.Contains("coin", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("credit", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("balance", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("quota", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatImageGenerationResult.Failure(
                        $"❌ A2E credits may be exhausted: {msg}\n\nCheck your balance at video.a2e.ai or set Image Generation provider to ComfyUI in Settings.");
                }

                var hint = "Start ComfyUI (e.g. http://localhost:8188) or check Settings → Image Generation.";
                if (ex is HttpRequestException)
                    return ChatImageGenerationResult.Failure($"❌ Image generation failed: {msg}\n\n{hint}");
                return ChatImageGenerationResult.Failure($"❌ Image generation failed: {msg}");
            }
        }

        private string ResolveImagePrompt(string conversationId, string userMessage)
        {
            if (IsImageFollowUpRequest(userMessage)
                && _lastPromptByConversation.TryGetValue(conversationId, out var last)
                && !string.IsNullOrWhiteSpace(last))
            {
                return last;
            }

            return ExtractImagePrompt(userMessage);
        }

        private static bool IsImageFollowUpRequest(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;
            var m = message.Trim();
            if (m.Equals("again", StringComparison.OrdinalIgnoreCase)
                || m.Equals("retry", StringComparison.OrdinalIgnoreCase)
                || m.Equals("one more", StringComparison.OrdinalIgnoreCase)
                || m.Equals("another", StringComparison.OrdinalIgnoreCase)
                || m.Equals("more", StringComparison.OrdinalIgnoreCase))
                return true;
            return m.Contains("try again", StringComparison.OrdinalIgnoreCase)
                || m.Contains("another one", StringComparison.OrdinalIgnoreCase)
                || m.Contains("one more", StringComparison.OrdinalIgnoreCase)
                || m.Contains("do it again", StringComparison.OrdinalIgnoreCase)
                || m.Contains("send another", StringComparison.OrdinalIgnoreCase)
                || m.Contains("another picture", StringComparison.OrdinalIgnoreCase)
                || m.Contains("another image", StringComparison.OrdinalIgnoreCase)
                || m.Contains("another photo", StringComparison.OrdinalIgnoreCase)
                || m.Contains("generate another", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractImagePrompt(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "a beautiful image";
            var m = message.Trim();
            var prefixes = new[]
            {
                "draw ", "draw a ", "draw an ",
                "generate image of ", "generate an image of ", "generate a picture of ", "generate image: ", "generate a picture: ",
                "create image of ", "create an image of ", "create a picture of ", "create image: ", "create a picture: ",
                "make an image of ", "make a picture of ",
                "send me a picture of ", "send me an image of ", "send me a photo of ",
                "send me a picture ", "send me an image ", "send me a photo ",
                "send a picture of ", "send an image of ",
                "show me a picture of ", "show me an image of ",
                "picture of ", "image of ", "photo of ",
                "generate ", "create ", "make ", "send me ", "show me "
            };
            foreach (var prefix in prefixes)
            {
                if (m.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var rest = m.Substring(prefix.Length).Trim();
                    if (!string.IsNullOrWhiteSpace(rest))
                        return rest;
                    break;
                }
            }

            return m;
        }

        private static async Task<string> SaveImageToConversationMediaAsync(string conversationId, byte[] imageBytes, string extension = ".png")
        {
            var mediaDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Media", conversationId);
            Directory.CreateDirectory(mediaDir);
            var storedFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.GetFullPath(Path.Combine(mediaDir, storedFileName));
            await File.WriteAllBytesAsync(fullPath, imageBytes).ConfigureAwait(false);
            return fullPath;
        }
    }

    public sealed class ChatImageGenerationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public byte[]? ImageBytes { get; init; }
        public string? ChatFilePath { get; init; }

        public static ChatImageGenerationResult Ok(string message, byte[] imageBytes, string chatFilePath) =>
            new()
            {
                Success = true,
                Message = message,
                ImageBytes = imageBytes,
                ChatFilePath = chatFilePath
            };

        public static ChatImageGenerationResult Failure(string message) =>
            new() { Success = false, Message = message };
    }
}
