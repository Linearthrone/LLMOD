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
