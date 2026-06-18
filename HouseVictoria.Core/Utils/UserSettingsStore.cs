using System.Reflection;
using HouseVictoria.Core.Models;
using Newtonsoft.Json;

namespace HouseVictoria.Core.Utils
{
    /// <summary>
    /// Persists user-edited settings outside the build output so they survive rebuilds and start.bat.
    /// </summary>
    public static class UserSettingsStore
    {
        private const string SettingsFileName = "user-settings.json";

        public static string GetSettingsFilePath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HouseVictoria");
            return Path.Combine(dir, SettingsFileName);
        }

        public static AppConfig? TryLoad()
        {
            try
            {
                var path = GetSettingsFilePath();
                if (!File.Exists(path))
                    return null;

                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<AppConfig>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UserSettingsStore.TryLoad: {ex.Message}");
                return null;
            }
        }

        public static void Save(AppConfig config)
        {
            var path = GetSettingsFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public static void MergeInto(AppConfig target, AppConfig source)
        {
            foreach (var prop in typeof(AppConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                var value = prop.GetValue(source);
                if (prop.PropertyType == typeof(List<string>))
                {
                    var list = value as List<string>;
                    prop.SetValue(target, list != null ? new List<string>(list) : new List<string>());
                    continue;
                }

                prop.SetValue(target, value);
            }
        }
    }
}
