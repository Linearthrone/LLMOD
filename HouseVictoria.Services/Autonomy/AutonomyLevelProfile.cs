using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Autonomy
{
    public static class AutonomyLevelProfile
    {
        public static bool IsActive(AppConfig config) =>
            config.EnableAutonomy && config.AutonomyLevel != AutonomyLevel.Off;

        public static int EffectiveMinIdleMinutes(AppConfig config) => config.AutonomyLevel switch
        {
            AutonomyLevel.Off => int.MaxValue,
            AutonomyLevel.Low => Math.Max(1, config.AutonomyMinIdleMinutes * 2),
            AutonomyLevel.Full => Math.Max(1, (config.AutonomyMinIdleMinutes + 1) / 2),
            _ => config.AutonomyMinIdleMinutes
        };

        public static int EffectiveMaxActionsPerHour(AppConfig config) => config.AutonomyLevel switch
        {
            AutonomyLevel.Off => 0,
            AutonomyLevel.Low => Math.Max(1, config.AutonomyMaxActionsPerHour / 2),
            AutonomyLevel.Full => Math.Max(2, (int)Math.Round(config.AutonomyMaxActionsPerHour * 1.5)),
            _ => config.AutonomyMaxActionsPerHour
        };

        public static int EffectiveMaxArtPerHour(AppConfig config) => config.AutonomyLevel switch
        {
            AutonomyLevel.Off => 0,
            AutonomyLevel.Low => Math.Max(0, config.AutonomyMaxArtPerHour / 2),
            AutonomyLevel.Full => Math.Max(1, (int)Math.Round(config.AutonomyMaxArtPerHour * 1.5)),
            _ => config.AutonomyMaxArtPerHour
        };

        public static int EffectiveTickIntervalSeconds(AppConfig config) => config.AutonomyLevel switch
        {
            AutonomyLevel.Off => int.MaxValue,
            AutonomyLevel.Low => (int)Math.Round(config.AutonomyTickIntervalSeconds * 1.35),
            AutonomyLevel.Full => Math.Max(30, (int)Math.Round(config.AutonomyTickIntervalSeconds * 0.75)),
            _ => config.AutonomyTickIntervalSeconds
        };

        public static string DisplayLabel(AutonomyLevel level) => level switch
        {
            AutonomyLevel.Off => "Off",
            AutonomyLevel.Low => "Low",
            AutonomyLevel.Mid => "Mid",
            AutonomyLevel.Full => "Full",
            _ => "Mid"
        };

        public static AutonomyLevel Cycle(AutonomyLevel current) => current switch
        {
            AutonomyLevel.Off => AutonomyLevel.Low,
            AutonomyLevel.Low => AutonomyLevel.Mid,
            AutonomyLevel.Mid => AutonomyLevel.Full,
            AutonomyLevel.Full => AutonomyLevel.Off,
            _ => AutonomyLevel.Mid
        };
    }
}
