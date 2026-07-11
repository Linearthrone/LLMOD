using System.IO;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.AIServices;
using HouseVictoria.Services.Persistence;

namespace HouseVictoria.Services.RemoteCompanion
{
    public sealed class RemoteCompanionMediaService
    {
        private readonly IAIService _aiService;
        private readonly DatabasePersistenceService _database;
        private readonly AppConfig _appConfig;
        private readonly string _mediaRoot;

        public RemoteCompanionMediaService(
            IAIService aiService,
            DatabasePersistenceService database,
            AppConfig appConfig)
        {
            _aiService = aiService;
            _database = database;
            _appConfig = appConfig;
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            _mediaRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HouseVictoria",
                "RemoteCompanionMedia");
            Directory.CreateDirectory(_mediaRoot);
        }

        public RemoteMediaModelsDto ListModels()
        {
            var imageModels = new List<RemoteMediaModelDto>();
            var videoModels = new List<RemoteMediaModelDto>();

            if (A2eImageGenerationClient.ShouldUseA2e("a2e", _appConfig.A2eApiToken))
            {
                imageModels.Add(new RemoteMediaModelDto { Id = "a2e", Label = "A2E Cloud", Provider = "a2e" });
                imageModels.Add(new RemoteMediaModelDto { Id = "general", Label = "A2E General", Provider = "a2e" });
                videoModels.Add(new RemoteMediaModelDto { Id = "a2e-video", Label = "A2E Video (beta)", Provider = "a2e" });
            }

            var checkpoint = string.IsNullOrWhiteSpace(_appConfig.ComfyUIPreferredCheckpoint)
                ? "sd_xl_base_1.0.safetensors"
                : _appConfig.ComfyUIPreferredCheckpoint.Trim();
            var checkpointLabel = checkpoint.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase)
                ? checkpoint[..^".safetensors".Length]
                : checkpoint;

            imageModels.Add(new RemoteMediaModelDto { Id = "comfyui", Label = "ComfyUI (local)", Provider = "comfyui" });
            imageModels.Add(new RemoteMediaModelDto { Id = "sdxl", Label = $"SDXL ({checkpointLabel})", Provider = "comfyui" });
            videoModels.Add(new RemoteMediaModelDto { Id = "comfyui-video", Label = "ComfyUI Video Workflow", Provider = "comfyui" });

            var defaultProvider = A2eImageGenerationClient.ShouldUseA2e(
                _appConfig.ImageGenerationProvider ?? "a2e",
                _appConfig.A2eApiToken)
                ? "a2e"
                : "comfyui";

            return new RemoteMediaModelsDto
            {
                Provider = defaultProvider,
                ImageModels = imageModels,
                VideoModels = videoModels
            };
        }

        public async Task<RemoteMediaGenerateResult> GenerateAsync(
            RemoteMediaGenerateRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.PositivePrompt))
                return RemoteMediaGenerateResult.Failure("positive_prompt is required");

            var mediaType = (request.MediaType ?? "image").Trim().ToLowerInvariant();
            if (mediaType == "video")
                return RemoteMediaGenerateResult.Failure(
                    "Video generation from the companion is not wired yet. Use image generation, or generate video on the PC in Settings → Image Generation.");

            var contact = await ResolvePrimaryContactAsync().ConfigureAwait(false);
            if (contact == null)
                return RemoteMediaGenerateResult.Failure("No AI contact found on the PC.");

            var prompt = BuildPrompt(request.PositivePrompt, request.NegativePrompt);
            var previousProvider = _appConfig.ImageGenerationProvider;
            var providerOverride = ResolveProviderForModel(request.Model);

            try
            {
                if (providerOverride != null)
                    _appConfig.ImageGenerationProvider = providerOverride;

                await using var stream = await _aiService.GenerateImageAsync(contact, prompt).ConfigureAwait(false);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
                var bytes = ms.ToArray();
                if (bytes.Length == 0)
                    return RemoteMediaGenerateResult.Failure("Image generation returned empty data.");

                var id = Guid.NewGuid().ToString("N");
                var ext = GuessExtension(bytes);
                var fileName = $"{id}{ext}";
                var filePath = Path.Combine(_mediaRoot, fileName);
                await File.WriteAllBytesAsync(filePath, bytes, cancellationToken).ConfigureAwait(false);

                var contentType = ext switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    ".gif" => "image/gif",
                    _ => "image/jpeg"
                };

                return RemoteMediaGenerateResult.Success(new RemoteMediaAssetDto
                {
                    Id = id,
                    MediaType = "image",
                    ContentType = contentType,
                    FileName = fileName,
                    PositivePrompt = request.PositivePrompt.Trim(),
                    NegativePrompt = request.NegativePrompt?.Trim(),
                    Model = request.Model?.Trim(),
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return RemoteMediaGenerateResult.Failure($"Generation failed: {ex.Message}");
            }
            finally
            {
                if (providerOverride != null)
                    _appConfig.ImageGenerationProvider = previousProvider;
            }
        }

        public (string Path, string ContentType)? TryGetMediaFile(string mediaId)
        {
            if (string.IsNullOrWhiteSpace(mediaId))
                return null;

            var safeId = Path.GetFileName(mediaId.Trim());
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".mp4", ".webm" })
            {
                var path = Path.Combine(_mediaRoot, safeId + ext);
                if (!File.Exists(path))
                    continue;

                var contentType = ext switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    ".gif" => "image/gif",
                    ".mp4" => "video/mp4",
                    ".webm" => "video/webm",
                    _ => "image/jpeg"
                };
                return (path, contentType);
            }

            return null;
        }

        private static string? ResolveProviderForModel(string? model)
        {
            var id = model?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return id switch
            {
                "comfyui" or "sdxl" or "comfyui-video" => "comfyui",
                "a2e" or "general" or "a2e-video" => "a2e",
                _ when id.StartsWith("a2e", StringComparison.Ordinal) => "a2e",
                _ when id.StartsWith("comfyui", StringComparison.Ordinal) => "comfyui",
                _ => null
            };
        }

        private static string BuildPrompt(string positive, string? negative)
        {
            var pos = positive.Trim();
            if (string.IsNullOrWhiteSpace(negative))
                return pos;
            return $"{pos}\n\nNegative prompt: {negative.Trim()}";
        }

        private async Task<AIContact?> ResolvePrimaryContactAsync()
        {
            try
            {
                var contacts = await _database.GetAllAsync<AIContact>().ConfigureAwait(false);
                if (contacts.Count == 0)
                    return null;

                if (!string.IsNullOrWhiteSpace(_appConfig.RemoteCompanionAiContactId)
                    && contacts.TryGetValue(_appConfig.RemoteCompanionAiContactId, out var configured))
                    return configured;

                return contacts.Values.FirstOrDefault(c => c.IsPrimaryAI) ?? contacts.Values.First();
            }
            catch
            {
                return null;
            }
        }

        private static string GuessExtension(byte[] bytes)
        {
            if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50)
                return ".png";
            if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
                return ".jpg";
            if (bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F')
                return ".webp";
            return ".jpg";
        }
    }

    public sealed class RemoteMediaModelsDto
    {
        public string Provider { get; init; } = string.Empty;
        public IReadOnlyList<RemoteMediaModelDto> ImageModels { get; init; } = Array.Empty<RemoteMediaModelDto>();
        public IReadOnlyList<RemoteMediaModelDto> VideoModels { get; init; } = Array.Empty<RemoteMediaModelDto>();
    }

    public sealed class RemoteMediaModelDto
    {
        public string Id { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Provider { get; init; } = string.Empty;
    }

    public sealed class RemoteMediaGenerateRequest
    {
        public string? MediaType { get; set; }
        public string PositivePrompt { get; set; } = string.Empty;
        public string? NegativePrompt { get; set; }
        public string? Model { get; set; }
    }

    public sealed class RemoteMediaAssetDto
    {
        public string Id { get; init; } = string.Empty;
        public string MediaType { get; init; } = "image";
        public string ContentType { get; init; } = "image/jpeg";
        public string FileName { get; init; } = string.Empty;
        public string? PositivePrompt { get; init; }
        public string? NegativePrompt { get; init; }
        public string? Model { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    public sealed class RemoteMediaGenerateResult
    {
        public bool IsSuccess { get; private init; }
        public string? Error { get; private init; }
        public RemoteMediaAssetDto? Asset { get; private init; }

        public static RemoteMediaGenerateResult Success(RemoteMediaAssetDto asset) =>
            new() { IsSuccess = true, Asset = asset };

        public static RemoteMediaGenerateResult Failure(string error) =>
            new() { IsSuccess = false, Error = error };
    }
}
