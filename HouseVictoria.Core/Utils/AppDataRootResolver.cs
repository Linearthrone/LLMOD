using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Utils
{
    /// <summary>
    /// Resolves relative data folders to a stable location (repo root when available, else next to the app exe).
    /// Prevents saves from landing in different folders depending on Debug/Release cwd or working directory.
    /// </summary>
    public static class AppDataRootResolver
    {
        public static string ResolveDataRoot(string appDirectory)
        {
            var dir = Path.GetFullPath(appDirectory);
            string? dataOrMediaRoot = null;

            // Walk up the full tree: prefer the repo (HouseVictoria.sln) over a nested Data/
            // folder under bin/Release, which would otherwise shadow the real data store.
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "HouseVictoria.sln")))
                    return dir;

                if (dataOrMediaRoot == null
                    && (Directory.Exists(Path.Combine(dir, "Media"))
                        || Directory.Exists(Path.Combine(dir, "Data"))))
                {
                    dataOrMediaRoot = dir;
                }

                var parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase))
                    break;

                dir = parent;
            }

            return dataOrMediaRoot ?? Path.GetFullPath(appDirectory);
        }

        public static string ResolveDataPath(string appDirectory, string relativeOrAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
                return Path.GetFullPath(Path.Combine(ResolveDataRoot(appDirectory), "Data"));

            if (Path.IsPathRooted(relativeOrAbsolutePath))
                return Path.GetFullPath(relativeOrAbsolutePath);

            var normalized = relativeOrAbsolutePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.GetFullPath(Path.Combine(ResolveDataRoot(appDirectory), normalized));
        }

        /// <summary>
        /// Rewrites paths that were persisted while running from bin/Debug or bin/Release
        /// back to the stable repo-root data folder.
        /// </summary>
        public static string CoerceDataPath(string appDirectory, string? path, string defaultRelative)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ResolveDataPath(appDirectory, defaultRelative);

            var full = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : ResolveDataPath(appDirectory, path);

            var binSegment = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
            if (!full.Contains(binSegment, StringComparison.OrdinalIgnoreCase))
                return full;

            var dataSegment = $"{Path.DirectorySeparatorChar}Data{Path.DirectorySeparatorChar}";
            var dataIdx = full.IndexOf(dataSegment, StringComparison.OrdinalIgnoreCase);
            if (dataIdx >= 0)
            {
                var relativeFromData = full[(dataIdx + 1)..]
                    .Replace(Path.DirectorySeparatorChar, '/');
                return ResolveDataPath(appDirectory, relativeFromData);
            }

            return ResolveDataPath(appDirectory, defaultRelative);
        }

        /// <summary>
        /// Store relative data paths in user-settings.json so rebuilds do not pin bin/Release folders.
        /// </summary>
        public static string ToPortableDataPath(string appDirectory, string resolvedPath, string defaultRelative)
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
                return defaultRelative.Replace('\\', '/');

            var root = ResolveDataRoot(appDirectory);
            var full = Path.GetFullPath(resolvedPath);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(root, full)
                    .Replace(Path.DirectorySeparatorChar, '/');
                return string.IsNullOrWhiteSpace(relative) ? defaultRelative.Replace('\\', '/') : relative;
            }

            return defaultRelative.Replace('\\', '/');
        }

        public static void SanitizeDataPathsForPersistence(string appDirectory, AppConfig config)
        {
            config.DataBankPath = ToPortableDataPath(appDirectory, config.DataBankPath, "Data/Databanks");
            config.LogsPath = ToPortableDataPath(appDirectory, config.LogsPath, "Logs");
            config.PersistentMemoryPath = ToPortableDataPath(appDirectory, config.PersistentMemoryPath, "Data/Memory");
            config.AutonomyDataPath = ToPortableDataPath(appDirectory, config.AutonomyDataPath, "Data/Autonomy");
            if (!Path.IsPathRooted(config.MediaPath))
            {
                config.MediaPath = config.MediaPath.Replace('\\', '/');
            }
            else
            {
                config.MediaPath = ToPortableDataPath(appDirectory, config.MediaPath, "Media");
            }
        }

        public static void ApplyCoercedDataPaths(string appDirectory, AppConfig config)
        {
            config.DataBankPath = CoerceDataPath(appDirectory, config.DataBankPath, "Data/Databanks");
            config.LogsPath = CoerceDataPath(appDirectory, config.LogsPath, "Logs");
            config.PersistentMemoryPath = CoerceDataPath(appDirectory, config.PersistentMemoryPath, "Data/Memory");
            config.AutonomyDataPath = CoerceDataPath(appDirectory, config.AutonomyDataPath, "Data/Autonomy");
            if (!Path.IsPathRooted(config.MediaPath))
            {
                config.MediaPath = Path.GetFullPath(
                    Path.Combine(ResolveDataRoot(appDirectory), config.MediaPath));
            }
            else
            {
                config.MediaPath = CoerceDataPath(appDirectory, config.MediaPath, "Media");
            }
        }

        /// <summary>
        /// Copies data from the exe-local shadow folder (bin/.../Data/...) into the resolved repo path
        /// when the shadow has content and the target is missing or empty.
        /// </summary>
        public static bool TryMigrateShadowDataFolder(string appDirectory, string relativeDataPath)
        {
            if (string.IsNullOrWhiteSpace(relativeDataPath))
                return false;

            var target = ResolveDataPath(appDirectory, relativeDataPath);
            var normalized = relativeDataPath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            var shadow = Path.GetFullPath(Path.Combine(Path.GetFullPath(appDirectory), normalized));

            if (string.Equals(target, shadow, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!Directory.Exists(shadow))
                return false;

            var shadowFiles = Directory.EnumerateFiles(shadow, "*", SearchOption.AllDirectories).Take(1).Any();
            if (!shadowFiles)
                return false;

            var targetHasData = Directory.Exists(target)
                && Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories).Take(1).Any();

            if (targetHasData)
                return false;

            try
            {
                Directory.CreateDirectory(target);
                CopyDirectory(shadow, target);
                System.Diagnostics.Debug.WriteLine($"AppDataRootResolver: migrated {shadow} -> {target}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AppDataRootResolver: migration failed {shadow} -> {target}: {ex.Message}");
                return false;
            }
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            foreach (var dir in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, dir);
                Directory.CreateDirectory(Path.Combine(targetDir, relative));
            }

            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, file);
                var dest = Path.Combine(targetDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: false);
            }
        }
    }
}
