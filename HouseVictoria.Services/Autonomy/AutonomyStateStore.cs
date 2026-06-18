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
        private readonly string _journalPath;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public AutonomyStateStore(string autonomyDataPath)
        {
            Directory.CreateDirectory(autonomyDataPath);
            _statePath = Path.Combine(autonomyDataPath, "runtime-state.json");
            _activityLogPath = Path.Combine(autonomyDataPath, "activity.log");
            _journalPath = Path.Combine(autonomyDataPath, "journal.jsonl");
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

        public async Task AppendJournalEntryAsync(AutonomyJournalEntry entry)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var json = JsonSerializer.Serialize(entry, JsonOptions);
                await File.AppendAllTextAsync(_journalPath, json + Environment.NewLine).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        public string JournalPath => _journalPath;
        public string ActivityLogPath => _activityLogPath;

        /// <summary>Reads activity.log entries within a rolling time window (local timestamps).</summary>
        public async Task<IReadOnlyList<AutonomyActionLogEntry>> ReadRecentActivityLogAsync(TimeSpan window)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!File.Exists(_activityLogPath))
                    return Array.Empty<AutonomyActionLogEntry>();

                var cutoff = DateTime.Now - window;
                var lines = await File.ReadAllLinesAsync(_activityLogPath).ConfigureAwait(false);
                var entries = new List<AutonomyActionLogEntry>();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var match = System.Text.RegularExpressions.Regex.Match(
                        line, @"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\] (.+)$");
                    if (!match.Success)
                        continue;

                    if (!DateTime.TryParse(match.Groups[1].Value, out var ts))
                        continue;

                    if (ts < cutoff)
                        continue;

                    var text = match.Groups[2].Value.Trim();
                    entries.Add(new AutonomyActionLogEntry
                    {
                        TimestampLocal = ts,
                        Text = text,
                        ActivityKind = TryParseActivityKind(text)
                    });
                }

                return entries.OrderByDescending(e => e.TimestampLocal).ToList();
            }
            catch
            {
                return Array.Empty<AutonomyActionLogEntry>();
            }
            finally
            {
                _lock.Release();
            }
        }

        private static AutonomyActivityKind? TryParseActivityKind(string text)
        {
            var colon = text.IndexOf(':');
            if (colon <= 0)
                return null;

            var prefix = text[..colon].Trim();
            return Enum.TryParse<AutonomyActivityKind>(prefix, ignoreCase: true, out var kind) ? kind : null;
        }
    }
}
