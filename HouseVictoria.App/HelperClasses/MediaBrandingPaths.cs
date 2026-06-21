using System.IO;

namespace HouseVictoria.App.HelperClasses
{
    /// <summary>
    /// Resolves House Victoria branding assets under <c>Media/Icons,Logos</c>.
    /// Walks up from the app directory so dev runs from <c>bin/</c> still find repo-root Media.
    /// </summary>
    public static class MediaBrandingPaths
    {
        private const string IconsLogosFolder = "Icons,Logos";
        private const string TrayIconFileName = "HouseVictoriaicon.ico";
        private const string ChatSealFileName = "HouseVictoriaSeal.jpeg";

        public static string? TrayIconPath => ResolveBrandingFile(TrayIconFileName);

        public static string? ChatSealPath => ResolveBrandingFile(ChatSealFileName);

        public static string? ResolveBrandingFile(string fileName)
        {
            foreach (var iconsDir in EnumerateIconsLogosDirectories())
            {
                var candidate = Path.Combine(iconsDir, fileName);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }

            var embedded = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources",
                fileName);
            return File.Exists(embedded) ? Path.GetFullPath(embedded) : null;
        }

        private static IEnumerable<string> EnumerateIconsLogosDirectories()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dir = AppDomain.CurrentDomain.BaseDirectory;

            while (!string.IsNullOrEmpty(dir))
            {
                foreach (var mediaRoot in GetMediaRoots(dir))
                {
                    var iconsDir = Path.Combine(mediaRoot, IconsLogosFolder);
                    if (seen.Add(iconsDir) && Directory.Exists(iconsDir))
                        yield return iconsDir;
                }

                var parent = Directory.GetParent(dir)?.FullName;
                if (parent == null || parent == dir)
                    break;
                dir = parent;
            }

            var currentDir = Environment.CurrentDirectory;
            if (!string.IsNullOrEmpty(currentDir))
            {
                foreach (var mediaRoot in GetMediaRoots(currentDir))
                {
                    var iconsDir = Path.Combine(mediaRoot, IconsLogosFolder);
                    if (seen.Add(iconsDir) && Directory.Exists(iconsDir))
                        yield return iconsDir;
                }
            }
        }

        private static IEnumerable<string> GetMediaRoots(string baseDir)
        {
            yield return Path.Combine(baseDir, "Media");
        }
    }
}
