namespace HouseVictoria.Services.Voice
{
    /// <summary>
    /// Lists Chatterbox Turbo reference voice ids from Media/ChatterboxVoices (*.wav stems).
    /// Used by persona editors to pick a call voice (stored in AIContact.CallVoiceId).
    /// </summary>
    public static class VoiceCatalog
    {
        private const string VoicesFolderName = "ChatterboxVoices";

        private static readonly string[] DefaultVoices = { "default" };

        public static IReadOnlyList<string> GetCallVoices(string? voicesDirectory = null)
        {
            var voicesDir = ResolveVoicesDirectory(voicesDirectory);
            if (voicesDir == null || !Directory.Exists(voicesDir))
                return DefaultVoices;

            var voices = Directory.EnumerateFiles(voicesDir, "*.wav", SearchOption.TopDirectoryOnly)
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return voices.Count > 0 ? voices : DefaultVoices;
        }

        [Obsolete("Use GetCallVoices")]
        public static IReadOnlyList<string> GetKokoroVoices(string? engineDirectory = null)
            => GetCallVoices(engineDirectory);

        private static string? ResolveVoicesDirectory(string? voicesDirectory)
        {
            if (!string.IsNullOrWhiteSpace(voicesDirectory))
            {
                var explicitPath = Path.IsPathRooted(voicesDirectory)
                    ? voicesDirectory
                    : Path.Combine(AppContext.BaseDirectory, voicesDirectory);
                if (Directory.Exists(explicitPath))
                    return explicitPath;
            }

            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, "Media", VoicesFolderName);
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
