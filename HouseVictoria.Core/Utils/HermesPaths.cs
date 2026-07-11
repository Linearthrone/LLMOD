using System.Diagnostics;

namespace HouseVictoria.Core.Utils
{
    /// <summary>
    /// Single source of truth for where Hermes keeps its config directory (config.yaml + .env).
    /// Resolution order matches Tools/HermesShared.ps1 so the C# gateway and the PowerShell setup
    /// scripts always agree on one location:
    ///   1. `hermes config path` (authoritative, whatever the installed Hermes reports)
    ///   2. %LOCALAPPDATA%\hermes (if a config.yaml already lives there)
    ///   3. %USERPROFILE%\.hermes (legacy default)
    /// </summary>
    public static class HermesPaths
    {
        /// <summary>Locates the hermes executable on PATH or in known install fallbacks.</summary>
        public static string? ResolveHermesExecutable()
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir.Trim(), OperatingSystem.IsWindows() ? "hermes.exe" : "hermes");
                if (File.Exists(candidate))
                    return candidate;
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var fallbacks = new[]
            {
                Path.Combine(localAppData, "hermes", "hermes-agent", ".venv", "Scripts", "hermes.exe"),
                Path.Combine(localAppData, "hermes", "hermes-agent", "venv", "Scripts", "hermes.exe"),
                Path.Combine(localAppData, "Programs", "hermes", "hermes.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "hermes")
            };

            foreach (var path in fallbacks)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        /// <summary>Resolves the Hermes config directory using the shared 3-step order.</summary>
        public static string ResolveHermesDir()
        {
            var exe = ResolveHermesExecutable();
            if (exe != null)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = "config path",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit(4000);
                        if (!string.IsNullOrWhiteSpace(output) && File.Exists(output))
                        {
                            var dir = Path.GetDirectoryName(output);
                            if (!string.IsNullOrWhiteSpace(dir))
                                return dir;
                        }
                    }
                }
                catch
                {
                    // Fall through to path-based fallbacks below.
                }
            }

            var localHermes = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hermes");
            if (File.Exists(Path.Combine(localHermes, "config.yaml")))
                return localHermes;

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hermes");
        }

        /// <summary>Full path to the resolved Hermes <c>.env</c> file.</summary>
        public static string ResolveEnvFile() => Path.Combine(ResolveHermesDir(), ".env");

        /// <summary>Full path to the resolved Hermes <c>config.yaml</c> file.</summary>
        public static string ResolveConfigFile() => Path.Combine(ResolveHermesDir(), "config.yaml");
    }
}
