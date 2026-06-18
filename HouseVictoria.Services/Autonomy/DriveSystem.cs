using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Autonomy
{
    /// <summary>
    /// Homeostatic drive model. Drives decay toward baseline, are consumed when satisfied,
    /// drift when idle, and bias autonomy decisions and tick intervals.
    /// </summary>
    internal static class DriveSystem
    {
        private const double DecayRate = 0.06;
        private const double BoredomDriftIdle = 0.05;

        private static readonly string[] DriveKeys =
            { "curiosity", "creativity", "social", "boredom", "purpose" };

        public static void Decay(AutonomyRuntimeState state, bool userQuiet, bool actedThisTick)
        {
            foreach (var key in DriveKeys)
            {
                var baseline = state.DriveBaselines.GetValueOrDefault(key, 0.5);
                var current = state.Drives.GetValueOrDefault(key, baseline);
                var next = current + (baseline - current) * DecayRate;
                state.Drives[key] = Clamp(next);
            }

            if (userQuiet && !actedThisTick)
                state.Drives["boredom"] = Clamp(state.Drives.GetValueOrDefault("boredom") + BoredomDriftIdle);

            if (userQuiet)
                state.Drives["social"] = Clamp(state.Drives.GetValueOrDefault("social") + 0.02);
        }

        /// <summary>Boost purpose drive while user guidance is active.</summary>
        public static void ApplyUserGuidanceBoost(AutonomyRuntimeState state)
        {
            if (string.IsNullOrWhiteSpace(state.UserGuidanceSuggestion))
                return;

            state.Drives["purpose"] = Clamp(state.Drives.GetValueOrDefault("purpose") + 0.15);
        }

        public static void Satisfy(AutonomyRuntimeState state, AutonomyActivityKind activity)
        {
            switch (activity)
            {
                case AutonomyActivityKind.CreateArt:
                    Reduce(state, "creativity", 0.35);
                    Reduce(state, "boredom", 0.30);
                    break;
                case AutonomyActivityKind.WriteResearch:
                    Reduce(state, "curiosity", 0.35);
                    Reduce(state, "boredom", 0.20);
                    break;
                case AutonomyActivityKind.WorkOnPriorityProject:
                case AutonomyActivityKind.AdvancePersonalProject:
                    Reduce(state, "purpose", 0.30);
                    Reduce(state, "boredom", 0.20);
                    break;
                case AutonomyActivityKind.Reflect:
                    Reduce(state, "boredom", 0.15);
                    break;
                case AutonomyActivityKind.ExploreEnvironment:
                    Reduce(state, "curiosity", 0.20);
                    Reduce(state, "social", 0.10);
                    Reduce(state, "boredom", 0.20);
                    break;
                case AutonomyActivityKind.ScanMarkets:
                case AutonomyActivityKind.ExecuteTrade:
                case AutonomyActivityKind.RunBacktest:
                    Reduce(state, "purpose", 0.25);
                    Reduce(state, "curiosity", 0.10);
                    break;
                case AutonomyActivityKind.GenerateGoal:
                    Reduce(state, "curiosity", 0.20);
                    Reduce(state, "boredom", 0.20);
                    break;
            }
        }

        public static (string Name, double Value) Dominant(AutonomyRuntimeState state)
        {
            var name = "boredom";
            var value = -1.0;
            foreach (var key in DriveKeys)
            {
                var v = state.Drives.GetValueOrDefault(key);
                if (v > value)
                {
                    value = v;
                    name = key;
                }
            }
            return (name, value);
        }

        public static string SuggestionHint(AutonomyRuntimeState state)
        {
            var weights = new List<(string Activity, double Weight)>
            {
                ("research", state.Drives.GetValueOrDefault("curiosity")),
                ("art", state.Drives.GetValueOrDefault("creativity")),
                ("project", state.Drives.GetValueOrDefault("purpose")),
                ("reflect", state.Drives.GetValueOrDefault("boredom") * 0.6 + state.Drives.GetValueOrDefault("social") * 0.4),
                ("environment", state.Drives.GetValueOrDefault("social"))
            };

            var ranked = weights
                .OrderByDescending(w => w.Weight)
                .Take(3)
                .Select(w => $"{w.Activity} ({w.Weight:F2})");

            return string.Join(" > ", ranked);
        }

        public static TimeSpan DynamicInterval(TimeSpan baseInterval, AutonomyRuntimeState state)
        {
            var restlessness = Math.Max(
                state.Drives.GetValueOrDefault("boredom"),
                state.Drives.GetValueOrDefault("curiosity"));
            var factor = 1.0 - 0.4 * Clamp(restlessness);
            var seconds = Math.Max(30, baseInterval.TotalSeconds * factor);
            return TimeSpan.FromSeconds(seconds);
        }

        private static void Reduce(AutonomyRuntimeState state, string key, double amount) =>
            state.Drives[key] = Clamp(state.Drives.GetValueOrDefault(key) - amount);

        private static double Clamp(double v) => Math.Max(0.0, Math.Min(1.0, v));
    }
}
