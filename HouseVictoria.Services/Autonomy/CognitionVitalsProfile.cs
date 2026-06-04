using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Autonomy
{
    internal static class CognitionVitalsProfile
    {
        public static CognitionVitalsSnapshot ForRhythm(CognitionVitalRhythm rhythm, string? label = null)
        {
            var (bpm, intensity, color, defaultLabel) = rhythm switch
            {
                CognitionVitalRhythm.TradingActive => (108.0, 0.95, "#FF6B35", "Active trading"),
                CognitionVitalRhythm.PriorityUrgent => (88.0, 0.85, "#FF4081", "High-priority focus"),
                CognitionVitalRhythm.ProjectWork => (76.0, 0.65, "#00E5FF", "Project work"),
                CognitionVitalRhythm.Environment => (72.0, 0.55, "#69F0AE", "Virtual environment"),
                CognitionVitalRhythm.Research => (58.0, 0.45, "#B388FF", "Deep research"),
                CognitionVitalRhythm.CreativeCalm => (50.0, 0.38, "#F48FB1", "Creative / ComfyUI"),
                CognitionVitalRhythm.Reflecting => (46.0, 0.32, "#81D4FA", "Reflecting"),
                CognitionVitalRhythm.Waiting => (42.0, 0.18, "#78909C", "Waiting for quiet"),
                CognitionVitalRhythm.Resting => (40.0, 0.15, "#546E7A", "At rest"),
                _ => (52.0, 0.3, "#4FC3F7", "Present")
            };

            return new CognitionVitalsSnapshot
            {
                Rhythm = rhythm,
                Label = label ?? defaultLabel,
                BeatsPerMinute = bpm,
                Intensity = intensity,
                WaveColorHex = color,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        public static CognitionVitalRhythm FromActivity(AutonomyActivityKind activity) => activity switch
        {
            AutonomyActivityKind.WorkOnPriorityProject => CognitionVitalRhythm.PriorityUrgent,
            AutonomyActivityKind.AdvancePersonalProject => CognitionVitalRhythm.ProjectWork,
            AutonomyActivityKind.CreateArt => CognitionVitalRhythm.CreativeCalm,
            AutonomyActivityKind.WriteResearch => CognitionVitalRhythm.Research,
            AutonomyActivityKind.Reflect => CognitionVitalRhythm.Reflecting,
            AutonomyActivityKind.ExploreEnvironment => CognitionVitalRhythm.Environment,
            AutonomyActivityKind.ExecuteTrade => CognitionVitalRhythm.TradingActive,
            AutonomyActivityKind.RunBacktest => CognitionVitalRhythm.Research,
            AutonomyActivityKind.ScanMarkets => CognitionVitalRhythm.TradingActive,
            AutonomyActivityKind.WaitingForUserQuiet => CognitionVitalRhythm.Waiting,
            AutonomyActivityKind.SkippedCooldown => CognitionVitalRhythm.Resting,
            _ => CognitionVitalRhythm.Resting
        };
    }
}
