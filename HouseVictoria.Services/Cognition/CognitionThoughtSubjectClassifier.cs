using System.Text.RegularExpressions;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Cognition
{
    internal static class CognitionThoughtSubjectClassifier
    {
        private static readonly string[] HighArousalTerms =
        {
            "excit", "amazing", "breakthrough", "finally", "stuck", "urgent", "love", "happy",
            "lesson", "maestro", "yes!", "wow", "great", "passion", "curious", "fascinat"
        };

        private static readonly string[] LowArousalTerms =
        {
            "junk", "spam", "routine", "mundane", "boring", "empty inbox", "no new", "nothing",
            "anticipated", "expected", "same as", "quiet", "at rest", "waiting"
        };

        internal sealed class SubjectProfile
        {
            public required string Id { get; init; }
            public required string Label { get; init; }
            public required string ColorHex { get; init; }
            public CognitionVitalRhythm Rhythm { get; init; }
            public double BaseBpm { get; init; }
            public double BaseIntensity { get; init; }
        }

        private static readonly Dictionary<string, SubjectProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["chat"] = new() { Id = "chat", Label = "Chat", ColorHex = "#FF6B9D", Rhythm = CognitionVitalRhythm.PriorityUrgent, BaseBpm = 78, BaseIntensity = 0.62 },
            ["maestro"] = new() { Id = "maestro", Label = "Maestro / lessons", ColorHex = "#7C4DFF", Rhythm = CognitionVitalRhythm.ProjectWork, BaseBpm = 82, BaseIntensity = 0.72 },
            ["desktop"] = new() { Id = "desktop", Label = "Desktop control", ColorHex = "#69F0AE", Rhythm = CognitionVitalRhythm.Environment, BaseBpm = 74, BaseIntensity = 0.58 },
            ["browser"] = new() { Id = "browser", Label = "Browser tab", ColorHex = "#40C4FF", Rhythm = CognitionVitalRhythm.ProjectWork, BaseBpm = 70, BaseIntensity = 0.52 },
            ["email"] = new() { Id = "email", Label = "Email", ColorHex = "#90A4AE", Rhythm = CognitionVitalRhythm.Waiting, BaseBpm = 44, BaseIntensity = 0.14 },
            ["trading"] = new() { Id = "trading", Label = "Trading", ColorHex = "#FF6B35", Rhythm = CognitionVitalRhythm.TradingActive, BaseBpm = 102, BaseIntensity = 0.88 },
            ["research"] = new() { Id = "research", Label = "Research", ColorHex = "#B388FF", Rhythm = CognitionVitalRhythm.Research, BaseBpm = 58, BaseIntensity = 0.42 },
            ["creative"] = new() { Id = "creative", Label = "Creative", ColorHex = "#F48FB1", Rhythm = CognitionVitalRhythm.CreativeCalm, BaseBpm = 54, BaseIntensity = 0.4 },
            ["autonomy"] = new() { Id = "autonomy", Label = "Autonomy", ColorHex = "#00E5FF", Rhythm = CognitionVitalRhythm.ProjectWork, BaseBpm = 68, BaseIntensity = 0.5 },
            ["tools"] = new() { Id = "tools", Label = "Tool loop", ColorHex = "#546E7A", Rhythm = CognitionVitalRhythm.Resting, BaseBpm = 56, BaseIntensity = 0.35 },
            ["reasoning"] = new() { Id = "reasoning", Label = "Reasoning", ColorHex = "#81D4FA", Rhythm = CognitionVitalRhythm.Reflecting, BaseBpm = 62, BaseIntensity = 0.48 }
        };

        public static SubjectProfile ResolveProfile(string subjectKey) =>
            Profiles.TryGetValue(subjectKey, out var p) ? p : Profiles["reasoning"];

        public static string ClassifyToolOrLine(string line)
        {
            var lower = line.ToLowerInvariant();

            if (Regex.IsMatch(lower, @"\b(maestro|lesson|course|module|quiz|training)\b"))
                return "maestro";
            if (Regex.IsMatch(lower, @"\b(gmail|email|inbox|mail)\b"))
                return "email";
            if (Regex.IsMatch(lower, @"\b(trade|mt4|alpaca|forex|position|market watch)\b"))
                return "trading";
            if (Regex.IsMatch(lower, @"\b(comfy|art|image gen|creative)\b"))
                return "creative";
            if (Regex.IsMatch(lower, @"\b(research|paper|study|deliverable)\b"))
                return "research";
            if (Regex.IsMatch(lower, @"\b(browser_capture|browser_tab|capture_tab)\b"))
                return "browser";
            if (Regex.IsMatch(lower, @"\b(computer_use|focus_desktop|click|scroll|keyboard|screenshot)\b"))
                return "desktop";
            if (Regex.IsMatch(lower, @"\b(autonomy|self-goal|project work)\b"))
                return "autonomy";
            if (Regex.IsMatch(lower, @"\b(tool_executor|tool_call|mcp__)\b"))
                return "tools";

            return "reasoning";
        }

        public static double ScoreArousal(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0.5;

            var lower = text.ToLowerInvariant();
            var high = HighArousalTerms.Count(t => lower.Contains(t, StringComparison.Ordinal));
            var low = LowArousalTerms.Count(t => lower.Contains(t, StringComparison.Ordinal));

            var score = 0.5 + high * 0.12 - low * 0.14;
            return Math.Clamp(score, 0.08, 1.0);
        }

        public static (double Bpm, double Intensity, double Weight) ApplyArousal(SubjectProfile profile, double arousal, int toolTurnBoost = 0)
        {
            var bpm = profile.BaseBpm;
            var intensity = profile.BaseIntensity;
            var weight = 0.45 + arousal * 0.4;

            if (profile.Id == "email")
            {
                bpm = arousal < 0.35 ? 42 : 52;
                intensity = arousal < 0.35 ? 0.12 : 0.22;
                weight = arousal < 0.35 ? 0.25 : 0.38;
            }
            else if (profile.Id == "maestro" || profile.Id == "chat")
            {
                bpm += (arousal - 0.5) * 36;
                intensity += (arousal - 0.5) * 0.45;
                weight = 0.55 + arousal * 0.35;
            }
            else
            {
                bpm += (arousal - 0.5) * 24;
                intensity += (arousal - 0.5) * 0.3;
            }

            if (toolTurnBoost > 0)
            {
                bpm += Math.Min(toolTurnBoost, 40) * 0.35;
                intensity += Math.Min(toolTurnBoost, 40) * 0.008;
                weight += Math.Min(toolTurnBoost, 20) * 0.015;
            }

            return (Math.Clamp(bpm, 38, 128), Math.Clamp(intensity, 0.08, 1.0), Math.Clamp(weight, 0.15, 1.0));
        }

        public static string TrimSnippet(string? text, int max = 72)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var oneLine = Regex.Replace(text.Trim(), @"\s+", " ");
            return oneLine.Length <= max ? oneLine : oneLine[..(max - 1)].TrimEnd() + "…";
        }
    }
}
