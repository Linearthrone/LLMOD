using System.Text.RegularExpressions;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Autonomy;

namespace HouseVictoria.Services.Cognition
{
    public sealed class CognitionThoughtStreamService : ICognitionThoughtStreamService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(750);
        private static readonly TimeSpan SubjectStaleAfter = TimeSpan.FromSeconds(50);
        private static readonly TimeSpan SubjectExpireAfter = TimeSpan.FromSeconds(100);
        private static readonly TimeSpan DecayInterval = TimeSpan.FromSeconds(1);

        private readonly object _gate = new();
        private readonly Dictionary<string, CognitionThoughtSubject> _subjects = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _sessionToolTurns = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _sessionSubjectKey = new(StringComparer.OrdinalIgnoreCase);

        private readonly string _hermesLogPath;
        private long _logOffset;
        private Timer? _pollTimer;
        private Timer? _decayTimer;
        private bool _disposed;
        private string? _chatContactName;
        private bool _chatTurnActive;
        private DateTime _lastRaiseUtc = DateTime.MinValue;
        private volatile bool _raisePending;

        private static readonly TimeSpan RaiseMinInterval = TimeSpan.FromMilliseconds(180);

        private static readonly Regex SessionTagRegex = new(@"\[(api-[a-f0-9]+)\]", RegexOptions.Compiled);
        private static readonly Regex ToolCompletedRegex = new(
            @"agent\.tool_executor:\s+tool\s+(\S+)\s+completed",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TurnEndedRegex = new(
            @"agent\.conversation_loop:\s+Turn ended:.*?tool_turns=(\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public CognitionThoughtStreamService(
            AppConfig config,
            IAgentDesktopMonitorService? desktopMonitor = null)
        {
            var mediaRoot = string.IsNullOrWhiteSpace(config.MediaPath)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media")
                : config.MediaPath;
            _ = mediaRoot;
            _hermesLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "hermes", "logs", "agent.log");

            if (desktopMonitor != null)
                desktopMonitor.ActivityAdded += OnDesktopActivity;

            _logOffset = File.Exists(_hermesLogPath) ? new FileInfo(_hermesLogPath).Length : 0;
            _pollTimer = new Timer(_ => PollHermesLog(), null, TimeSpan.FromSeconds(1), PollInterval);
            _decayTimer = new Timer(_ => DecaySubjects(), null, DecayInterval, DecayInterval);
        }

        public event EventHandler<CognitionThoughtStreamChangedEventArgs>? StreamChanged;

        public IReadOnlyList<CognitionThoughtSubject> GetActiveSubjects()
        {
            lock (_gate)
            {
                var list = _subjects.Values
                    .OrderByDescending(s => s.AttentionWeight)
                    .ThenByDescending(s => s.LastPulseUtc)
                    .ToList();

                if (list.Count > 1)
                    list = list.Where(s => !string.Equals(s.Id, "resting", StringComparison.OrdinalIgnoreCase)).ToList();

                return list.Take(5).Select(CloneSubject).ToList();
            }
        }

        public string? GetStreamCaption()
        {
            var subjects = GetActiveSubjects();
            if (subjects.Count == 0)
                return null;
            return string.Join(" · ", subjects.Select(s => s.Label));
        }

        public string? GetLatestSnippet()
        {
            lock (_gate)
            {
                return _subjects.Values
                    .OrderByDescending(s => s.LastPulseUtc)
                    .Select(s => s.LatestSnippet)
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
            }
        }

        public void NotifyChatTurnStarted(string? contactName)
        {
            _chatContactName = contactName;
            _chatTurnActive = true;
            var label = string.IsNullOrWhiteSpace(contactName) ? "Chat" : $"Chat · {contactName}";
            PulseSubject("chat", label, "Listening…", arousal: 0.72, toolTurnBoost: 2);
        }

        public void NotifyChatTurnEnded()
        {
            _chatTurnActive = false;
            PulseSubject("chat", _chatContactName == null ? "Chat" : $"Chat · {_chatContactName}",
                "Reply ready", arousal: 0.55, toolTurnBoost: 0);
        }

        public void NotifyThoughtSnippet(string subjectId, string label, string snippet, double? arousalHint = null)
        {
            PulseSubject(subjectId, label, snippet, arousalHint ?? 0.55);
        }

        public void NotifyStreamDelta(string deltaText, string accumulatedSegment)
        {
            if (string.IsNullOrWhiteSpace(deltaText) && string.IsNullOrWhiteSpace(accumulatedSegment))
                return;

            var text = accumulatedSegment;
            var subjectKey = CognitionThoughtSubjectClassifier.ClassifyToolOrLine(text);
            if (subjectKey == "reasoning" && _chatTurnActive)
                subjectKey = "chat";

            var profile = CognitionThoughtSubjectClassifier.ResolveProfile(subjectKey);
            var arousal = Math.Min(1.0, CognitionThoughtSubjectClassifier.ScoreArousal(text) + 0.18);
            var label = _chatTurnActive && subjectKey == "chat"
                ? (_chatContactName == null ? "Chat · thinking" : $"Chat · {_chatContactName}")
                : $"{profile.Label} · thinking";

            PulseSubject(subjectKey, label, text, arousal, toolTurnBoost: 5, immediate: false);
        }

        public void NotifyHermesToolProgress(string toolName, string label, string status)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                return;

            var subjectKey = CognitionThoughtSubjectClassifier.ClassifyToolOrLine(toolName + " " + label);
            var profile = CognitionThoughtSubjectClassifier.ResolveProfile(subjectKey);
            var running = string.Equals(status, "running", StringComparison.OrdinalIgnoreCase);
            var arousal = running ? 0.78 : 0.5;
            var snippet = running
                ? CognitionThoughtSubjectClassifier.TrimSnippet(label)
                : $"Done: {CognitionThoughtSubjectClassifier.TrimSnippet(SimplifyToolName(toolName), 48)}";

            PulseSubject(subjectKey, profile.Label, snippet, arousal, toolTurnBoost: running ? 6 : 1, immediate: running);

            if (_chatTurnActive)
            {
                PulseSubject("chat",
                    _chatContactName == null ? "Chat" : $"Chat · {_chatContactName}",
                    running ? $"→ {snippet}" : "Continuing…",
                    arousal: running ? 0.7 : 0.55,
                    toolTurnBoost: running ? 3 : 0,
                    immediate: false);
            }
        }

        public void NotifyAutonomyVitals(CognitionVitalsSnapshot vitals)
        {
            var profile = CognitionVitalsProfile.ForRhythm(vitals.Rhythm, vitals.Label);
            var arousal = Math.Clamp(vitals.Intensity + 0.15, 0.2, 1.0);
            PulseSubject(
                "autonomy",
                string.IsNullOrWhiteSpace(vitals.Label) ? "Autonomy" : vitals.Label,
                vitals.LastActivitySummary ?? vitals.Label,
                arousal,
                toolTurnBoost: 0,
                overrideRhythm: profile.Rhythm,
                overrideBpm: profile.BeatsPerMinute,
                overrideIntensity: profile.Intensity,
                overrideColor: profile.WaveColorHex);
        }

        private void OnDesktopActivity(object? sender, AgentDesktopActivityEntry entry)
        {
            var subjectKey = CognitionThoughtSubjectClassifier.ClassifyToolOrLine(entry.Text);
            var profile = CognitionThoughtSubjectClassifier.ResolveProfile(subjectKey);
            var arousal = CognitionThoughtSubjectClassifier.ScoreArousal(entry.Text);
            var snippet = CognitionThoughtSubjectClassifier.TrimSnippet(entry.Text);
            PulseSubject(subjectKey, profile.Label, snippet, arousal, toolTurnBoost: 1);
        }

        private void PollHermesLog()
        {
            if (_disposed || !File.Exists(_hermesLogPath))
                return;

            try
            {
                using var stream = new FileStream(_hermesLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (_logOffset > stream.Length)
                    _logOffset = 0;

                stream.Seek(_logOffset, SeekOrigin.Begin);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null)
                    ProcessLogLine(line);

                _logOffset = stream.Position;
            }
            catch
            {
                // Transient file lock while gateway writes.
            }
        }

        private void ProcessLogLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            var sessionMatch = SessionTagRegex.Match(line);
            var sessionId = sessionMatch.Success ? sessionMatch.Groups[1].Value : null;

            var toolMatch = ToolCompletedRegex.Match(line);
            if (toolMatch.Success)
            {
                var toolName = toolMatch.Groups[1].Value;
                var subjectKey = CognitionThoughtSubjectClassifier.ClassifyToolOrLine(toolName + " " + line);
                if (sessionId != null)
                {
                    _sessionSubjectKey[sessionId] = subjectKey;
                    _sessionToolTurns.TryGetValue(sessionId, out var turns);
                    _sessionToolTurns[sessionId] = turns + 1;
                    turns = _sessionToolTurns[sessionId];
                    var profile = CognitionThoughtSubjectClassifier.ResolveProfile(subjectKey);
                    var arousal = CognitionThoughtSubjectClassifier.ScoreArousal(line);
                    var snippet = $"Tool: {SimplifyToolName(toolName)}";
                    PulseSubject(subjectKey, profile.Label, snippet, arousal, toolTurnBoost: turns);
                }
                else
                {
                    var profile = CognitionThoughtSubjectClassifier.ResolveProfile(subjectKey);
                    PulseSubject(subjectKey, profile.Label, $"Tool: {SimplifyToolName(toolName)}",
                        CognitionThoughtSubjectClassifier.ScoreArousal(line), toolTurnBoost: 1);
                }

                if (_chatTurnActive)
                    PulseSubject("chat", _chatContactName == null ? "Chat" : $"Chat · {_chatContactName}",
                        $"Using {SimplifyToolName(toolName)}…", arousal: 0.68, toolTurnBoost: 2);
                return;
            }

            var turnMatch = TurnEndedRegex.Match(line);
            if (turnMatch.Success && sessionId != null)
            {
                _sessionToolTurns.TryGetValue(sessionId, out var turns);
                if (turns <= 0 && int.TryParse(turnMatch.Groups[1].Value, out var parsed))
                    turns = parsed;

                if (_sessionSubjectKey.TryGetValue(sessionId, out var key))
                {
                    var profile = CognitionThoughtSubjectClassifier.ResolveProfile(key);
                    var arousal = turns > 8 ? 0.75 : turns > 2 ? 0.58 : 0.42;
                    PulseSubject(key, profile.Label, turns > 0 ? $"Turn done ({turns} tools)" : "Turn complete",
                        arousal, toolTurnBoost: Math.Min(turns, 25));
                }

                _sessionToolTurns.Remove(sessionId);
                _sessionSubjectKey.Remove(sessionId);
            }
        }

        private static string SimplifyToolName(string raw)
        {
            var name = raw;
            if (name.StartsWith("mcp__", StringComparison.OrdinalIgnoreCase))
                name = name["mcp__".Length..];
            var parts = name.Split("__", 2);
            return parts.Length == 2 ? parts[1] : name;
        }

        private void PulseSubject(
            string subjectKey,
            string label,
            string? snippet,
            double arousal,
            int toolTurnBoost = 0,
            CognitionVitalRhythm? overrideRhythm = null,
            double? overrideBpm = null,
            double? overrideIntensity = null,
            string? overrideColor = null,
            bool immediate = true)
        {
            var profile = CognitionThoughtSubjectClassifier.ResolveProfile(subjectKey);
            var (bpm, intensity, weight) = CognitionThoughtSubjectClassifier.ApplyArousal(profile, arousal, toolTurnBoost);

            if (overrideRhythm.HasValue)
            {
                bpm = overrideBpm ?? bpm;
                intensity = overrideIntensity ?? intensity;
            }

            var subject = new CognitionThoughtSubject
            {
                Id = subjectKey,
                Label = label,
                LatestSnippet = CognitionThoughtSubjectClassifier.TrimSnippet(snippet),
                Rhythm = overrideRhythm ?? profile.Rhythm,
                BeatsPerMinute = bpm,
                Intensity = intensity,
                AttentionWeight = weight,
                WaveColorHex = overrideColor ?? profile.ColorHex,
                LastPulseUtc = DateTime.UtcNow
            };

            lock (_gate)
                _subjects[subjectKey] = subject;

            RaiseChanged(immediate);
        }

        private void FlushPendingRaise()
        {
            if (!_raisePending)
                return;
            RaiseChanged(immediate: true);
        }

        private void DecaySubjects()
        {
            if (_disposed)
                return;

            var now = DateTime.UtcNow;
            var changed = false;

            lock (_gate)
            {
                var keys = _subjects.Keys.ToList();
                foreach (var key in keys)
                {
                    var s = _subjects[key];
                    var age = now - s.LastPulseUtc;
                    if (age >= SubjectExpireAfter)
                    {
                        _subjects.Remove(key);
                        changed = true;
                        continue;
                    }

                    if (age >= SubjectStaleAfter)
                    {
                        s.Intensity *= 0.92;
                        s.BeatsPerMinute = Math.Max(40, s.BeatsPerMinute * 0.97);
                        s.AttentionWeight *= 0.95;
                        changed = true;
                    }
                }

                if (_subjects.Count == 0 && !_chatTurnActive)
                {
                    var resting = new CognitionThoughtSubject
                    {
                        Id = "resting",
                        Label = "At rest",
                        LatestSnippet = null,
                        Rhythm = CognitionVitalRhythm.Resting,
                        BeatsPerMinute = 42,
                        Intensity = 0.12,
                        AttentionWeight = 0.2,
                        WaveColorHex = "#546E7A",
                        LastPulseUtc = now
                    };
                    _subjects["resting"] = resting;
                    changed = true;
                }
            }

            if (changed)
                RaiseChanged(immediate: true);

            FlushPendingRaise();
        }

        private void RaiseChanged(bool immediate = true)
        {
            var now = DateTime.UtcNow;
            if (!immediate && now - _lastRaiseUtc < RaiseMinInterval)
            {
                _raisePending = true;
                return;
            }

            _lastRaiseUtc = now;
            _raisePending = false;
            var subjects = GetActiveSubjects();
            StreamChanged?.Invoke(this, new CognitionThoughtStreamChangedEventArgs
            {
                Subjects = subjects,
                Caption = subjects.Count == 0 ? "At rest" : string.Join(" · ", subjects.Select(s => s.Label)),
                LatestSnippet = GetLatestSnippet()
            });
        }

        private static CognitionThoughtSubject CloneSubject(CognitionThoughtSubject s) => new()
        {
            Id = s.Id,
            Label = s.Label,
            LatestSnippet = s.LatestSnippet,
            Rhythm = s.Rhythm,
            BeatsPerMinute = s.BeatsPerMinute,
            Intensity = s.Intensity,
            AttentionWeight = s.AttentionWeight,
            WaveColorHex = s.WaveColorHex,
            LastPulseUtc = s.LastPulseUtc
        };

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _pollTimer?.Dispose();
            _decayTimer?.Dispose();
        }
    }
}
