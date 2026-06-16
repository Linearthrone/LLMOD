namespace HouseVictoria.Services.Voice
{
    /// <summary>
    /// Lists Kokoro voice ids from the on-device speech-to-speech engine's voice pack folder.
    /// Used by persona editors to pick a call voice (stored in AIContact.PiperVoiceId).
    /// </summary>
    public static class VoiceCatalog
    {
        private const string EngineFolderName = "On-Device-Speech-to-Speech-Conversational-AI";

        private static readonly string[] DefaultVoices =
        {
            "af_nicole", "af_bella", "af_sarah", "af_sky", "af_heart", "af_jessica",
            "am_adam", "am_michael"
        };

        public static IReadOnlyList<string> GetKokoroVoices(string? engineDirectory = null)
        {
            var voicesDir = ResolveVoicesDirectory(engineDirectory);
            if (voicesDir == null || !Directory.Exists(voicesDir))
                return DefaultVoices;

            var voices = Directory.EnumerateFiles(voicesDir, "*.pt", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name)
                    && !string.Equals(name, "weights", StringComparison.OrdinalIgnoreCase)
                    && (name!.StartsWith("af_", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("am_", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return voices.Count > 0 ? voices : DefaultVoices;
        }

        private static string? ResolveVoicesDirectory(string? engineDirectory)
        {
            if (!string.IsNullOrWhiteSpace(engineDirectory))
            {
                var explicitPath = Path.Combine(engineDirectory, "data", "voices");
                if (Directory.Exists(explicitPath))
                    return explicitPath;
            }

            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, EngineFolderName, "data", "voices");
                if (Directory.Exists(candidate))
                    return candidate;

                var parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }

            return null;
        }
    }
}
