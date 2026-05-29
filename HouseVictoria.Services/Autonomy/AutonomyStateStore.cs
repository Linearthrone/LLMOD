using System.Text.Json;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Autonomy
{
    internal sealed class AutonomyStateStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly string _statePath;
        private readonly string _activityLogPath;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public AutonomyStateStore(string autonomyDataPath)
        {
            Directory.CreateDirectory(autonomyDataPath);
            _statePath = Path.Combine(autonomyDataPath, "runtime-state.json");
            _activityLogPath = Path.Combine(autonomyDataPath, "activity.log");
        }

        public async Task<AutonomyRuntimeState> LoadStateAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!File.Exists(_statePath))
                    return new AutonomyRuntimeState();

                var json = await File.ReadAllTextAsync(_statePath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<AutonomyRuntimeState>(json, JsonOptions)
                       ?? new AutonomyRuntimeState();
            }
            catch
            {
                return new AutonomyRuntimeState();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveStateAsync(AutonomyRuntimeState state)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var json = JsonSerializer.Serialize(state, JsonOptions);
                await File.WriteAllTextAsync(_statePath, json).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task AppendActivityLogAsync(string line)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}";
                await File.AppendAllTextAsync(_activityLogPath, entry).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
