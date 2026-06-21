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
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "HouseVictoria.sln"))
                    || Directory.Exists(Path.Combine(dir, "Media"))
                    || Directory.Exists(Path.Combine(dir, "Data")))
                {
                    return dir;
                }

                var parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, dir, StringComparison.OrdinalIgnoreCase))
                    break;

                dir = parent;
            }

            return Path.GetFullPath(appDirectory);
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
    }
}
