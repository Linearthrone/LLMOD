using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Autonomy
{
    /// <summary>
    /// Homeostatic drive model. Drives are no longer cosmetic: they decay toward a
    /// resting baseline every tick, are *consumed* when an activity satisfies them,
    /// drift (boredom) when the agent sits idle, and bias both what she chooses to do
    /// and how often she wakes up to do it.
    /// </summary>
    internal static class DriveSystem
    {
        private const double DecayRate = 0.06;      // pull toward baseline per tick
        private const double BoredomDriftIdle = 0.05; // boredom climbs when idle and not acting

        private static readonly string[] DriveKeys =
            { "curiosity", "creativity", "social", "boredom", "industry" };

        /// <summary>Move drives toward their baseline; boredom drifts up while idle.</summary>
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

            // Quiet for a while means the social drive slowly builds (she misses contact);
            // when the user is around it relaxes toward baseline (handled by decay above).
            if (userQuiet)
                state.Drives["social"] = Clamp(state.Drives.GetValueOrDefault("social") + 0.02);
        }

        /// <summary>Consume the drives an activity satisfies once it has been performed.</summary>
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
                    Reduce(state, "industry", 0.30);
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
                    Reduce(state, "industry", 0.25);
                    Reduce(state, "curiosity", 0.10);
                    break;
                case AutonomyActivityKind.GenerateGoal:
                    Reduce(state, "curiosity", 0.20);
                    Reduce(state, "boredom", 0.20);
                    break;
            }
        }

        /// <summary>The single strongest drive right now (used for goal-generation gating).</summary>
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

        /// <summary>
        /// Drive-weighted, human-readable hint of which activities appeal most right now,
        /// fed into the decision prompt so the LLM's choice reflects her internal state.
        /// </summary>
        public static string SuggestionHint(AutonomyRuntimeState state)
        {
            var weights = new List<(string Activity, double Weight)>
            {
                ("research", state.Drives.GetValueOrDefault("curiosity")),
                ("art", state.Drives.GetValueOrDefault("creativity")),
                ("project", state.Drives.GetValueOrDefault("industry")),
                ("reflect", state.Drives.GetValueOrDefault("boredom") * 0.6 + state.Drives.GetValueOrDefault("social") * 0.4),
                ("environment", state.Drives.GetValueOrDefault("social"))
            };

            var ranked = weights
                .OrderByDescending(w => w.Weight)
                .Take(3)
                .Select(w => $"{w.Activity} ({w.Weight:F2})");

            return string.Join(" > ", ranked);
        }

        /// <summary>
        /// Shorten the wait between ticks when boredom or curiosity run high, so she
        /// gets restless and acts sooner; never below 30s, never above the configured base.
        /// </summary>
        public static TimeSpan DynamicInterval(TimeSpan baseInterval, AutonomyRuntimeState state)
        {
            var restlessness = Math.Max(
                state.Drives.GetValueOrDefault("boredom"),
                state.Drives.GetValueOrDefault("curiosity"));
            var factor = 1.0 - 0.4 * Clamp(restlessness); // up to 40% faster
            var seconds = Math.Max(30, baseInterval.TotalSeconds * factor);
            return TimeSpan.FromSeconds(seconds);
        }

        private static void Reduce(AutonomyRuntimeState state, string key, double amount) =>
            state.Drives[key] = Clamp(state.Drives.GetValueOrDefault(key) - amount);

        private static double Clamp(double v) => Math.Max(0.0, Math.Min(1.0, v));
    }
}
