namespace HouseVictoria.Services.Trading
{
    /// <summary>
    /// Resolves MT4 install paths to the writable per-terminal data directory under AppData.
    /// </summary>
    public static class Mt4PathResolver
    {
        private static readonly HashSet<string> NonTerminalFolderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Common", "Community", "Help"
        };

        public static string Resolve(string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var normalized = Path.GetFullPath(configuredPath.Trim());

                if (IsWritableTerminalDataPath(normalized))
                    return normalized;

                var fromOrigin = FindTerminalByOrigin(normalized);
                if (fromOrigin != null)
                    return fromOrigin;
            }

            var best = FindBestTerminalWithBridge();
            if (best != null)
                return best;

            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var normalized = Path.GetFullPath(configuredPath.Trim());
                if (Directory.Exists(normalized))
                    return normalized;
            }

            throw new DirectoryNotFoundException(
                "Could not resolve an MT4 terminal data folder. Set MT4DataPath to your MT4 install path " +
                "or AppData\\Roaming\\MetaQuotes\\Terminal\\<id>, then ensure MT4 has been run at least once.");
        }

        public static bool TryResolve(string? configuredPath, out string resolvedPath)
        {
            try
            {
                resolvedPath = Resolve(configuredPath);
                return true;
            }
            catch
            {
                resolvedPath = string.Empty;
                return false;
            }
        }

        public static bool IsWritableTerminalDataPath(string path)
        {
            var mql4Path = Path.Combine(path, "MQL4");
            if (!Directory.Exists(mql4Path))
                return false;

            var filesPath = Path.Combine(mql4Path, "Files");
            try
            {
                Directory.CreateDirectory(filesPath);
                var probe = Path.Combine(filesPath, ".hv_write_probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string? FindTerminalByOrigin(string installOrDataPath)
        {
            var terminalsRoot = GetTerminalsRoot();
            if (terminalsRoot == null)
                return null;

            var target = installOrDataPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var dir in Directory.EnumerateDirectories(terminalsRoot))
            {
                var folderName = Path.GetFileName(dir);
                if (NonTerminalFolderNames.Contains(folderName))
                    continue;

                var originFile = Path.Combine(dir, "origin.txt");
                if (!File.Exists(originFile))
                    continue;

                var origin = File.ReadAllText(originFile).Trim()
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (!string.Equals(origin, target, StringComparison.OrdinalIgnoreCase))
                    continue;

                var resolved = Path.GetFullPath(dir);
                if (Directory.Exists(Path.Combine(resolved, "MQL4")))
                    return resolved;
            }

            return null;
        }

        public static string? FindBestTerminalWithBridge()
        {
            var terminalsRoot = GetTerminalsRoot();
            if (terminalsRoot == null)
                return null;

            string? bestTerminal = null;
            DateTime bestActivity = DateTime.MinValue;

            foreach (var dir in Directory.EnumerateDirectories(terminalsRoot))
            {
                var folderName = Path.GetFileName(dir);
                if (NonTerminalFolderNames.Contains(folderName))
                    continue;

                var bridgeFolder = Path.Combine(dir, "MQL4", "Files", "HouseVictoria");
                var expertsEa = Path.Combine(dir, "MQL4", "Experts", "HouseVictoriaBridge.ex4");
                var expertsMq4 = Path.Combine(dir, "MQL4", "Experts", "HouseVictoriaBridge.mq4");

                if (!Directory.Exists(bridgeFolder) && !File.Exists(expertsEa) && !File.Exists(expertsMq4))
                    continue;

                var activity = GetLatestWriteTime(dir, bridgeFolder, expertsEa, expertsMq4);
                if (activity > bestActivity)
                {
                    bestActivity = activity;
                    bestTerminal = Path.GetFullPath(dir);
                }
            }

            return bestTerminal;
        }

        private static string? GetTerminalsRoot()
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MetaQuotes",
                "Terminal");

            return Directory.Exists(root) ? root : null;
        }

        private static DateTime GetLatestWriteTime(string terminalDir, string bridgeFolder, string expertsEa, string expertsMq4)
        {
            var latest = Directory.GetLastWriteTimeUtc(terminalDir);

            if (File.Exists(expertsEa))
                latest = MaxUtc(latest, File.GetLastWriteTimeUtc(expertsEa));
            if (File.Exists(expertsMq4))
                latest = MaxUtc(latest, File.GetLastWriteTimeUtc(expertsMq4));

            if (Directory.Exists(bridgeFolder))
            {
                foreach (var file in Directory.EnumerateFiles(bridgeFolder, "*", SearchOption.AllDirectories))
                {
                    latest = MaxUtc(latest, File.GetLastWriteTimeUtc(file));
                }
            }

            return latest;
        }

        private static DateTime MaxUtc(DateTime a, DateTime b) => a >= b ? a : b;
    }
}
