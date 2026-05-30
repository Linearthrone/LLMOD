namespace HouseVictoria.Services.Trading
{
    /// <summary>
    /// Ensures the HouseVictoriaBridge expert advisor source is present in the MT4 terminal folder.
    /// </summary>
    public static class Mt4BridgeInstaller
    {
        public const string ExpertFileName = "HouseVictoriaBridge.mq4";

        public static bool EnsureExpertAdvisor(string terminalDataPath, string? additionalSourceRoot = null)
        {
            var expertsDir = Path.Combine(terminalDataPath, "MQL4", "Experts");
            Directory.CreateDirectory(expertsDir);

            var targetPath = Path.Combine(expertsDir, ExpertFileName);
            if (File.Exists(targetPath))
                return true;

            var sourcePath = FindSourceExpertPath(additionalSourceRoot);
            if (sourcePath == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"MT4 bridge EA not found in Experts and no source {ExpertFileName} to copy.");
                return false;
            }

            File.Copy(sourcePath, targetPath, overwrite: false);
            System.Diagnostics.Debug.WriteLine($"Copied MT4 bridge EA to: {targetPath}");
            return true;
        }

        public static string? FindSourceExpertPath(string? additionalSourceRoot = null)
        {
            return GetCandidateSourcePaths(additionalSourceRoot).FirstOrDefault(File.Exists);
        }

        public static IReadOnlyList<string> GetCandidateSourcePaths(string? additionalSourceRoot = null)
        {
            var candidates = new List<string>();

            void Add(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                var full = Path.GetFullPath(path);
                if (!candidates.Contains(full, StringComparer.OrdinalIgnoreCase))
                    candidates.Add(full);
            }

            if (!string.IsNullOrWhiteSpace(additionalSourceRoot))
            {
                Add(Path.Combine(additionalSourceRoot, "MT4Bridge", ExpertFileName));
                Add(Path.Combine(additionalSourceRoot, ExpertFileName));
            }

            Add(Path.Combine(AppContext.BaseDirectory, "MT4Bridge", ExpertFileName));

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var depth = 0; depth < 8 && dir != null; depth++, dir = dir.Parent)
            {
                Add(Path.Combine(dir.FullName, "MT4Bridge", ExpertFileName));
            }

            return candidates;
        }
    }
}
