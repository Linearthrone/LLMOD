using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Trading
{
    /// <summary>
    /// Ensures a high-priority autonomy project exists for multi-pair market monitoring.
    /// </summary>
    public static class MarketWatchProjectBootstrap
    {
        public const string ProjectName = "MT4 Market Watch";

        public static async Task<Project?> EnsureAsync(
            IProjectManagementService projects,
            AppConfig config,
            CancellationToken cancellationToken = default)
        {
            var targetPriority = Math.Max(config.AutonomyHighPriorityThreshold, config.TradingWatchProjectPriority);

            var all = await projects.GetAllProjectsAsync().ConfigureAwait(false);
            var existing = all.FirstOrDefault(p =>
                p.Name.Equals(ProjectName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                var changed = false;
                if (existing.Priority < targetPriority)
                {
                    existing.Priority = targetPriority;
                    changed = true;
                }

                if (existing.Phase == ProjectPhase.Completed)
                {
                    existing.Phase = ProjectPhase.Development;
                    existing.CompletionPercentage = Math.Min(existing.CompletionPercentage, 85);
                    changed = true;
                }

                if (changed)
                {
                    existing.LastModifiedAt = DateTime.Now;
                    return await projects.UpdateProjectAsync(existing).ConfigureAwait(false);
                }

                return existing;
            }

            return await projects.CreateProjectAsync(new Project
            {
                Name = ProjectName,
                Type = ProjectType.Research,
                Description =
                    "Autonomous multi-pair FX/CFD monitoring via House Victoria MT4 bridge. " +
                    "Tracks watchlist quotes, technical signals (RSI/MACD/MA on H1), and routes " +
                    "opportunities to backtest/trade blocks. Symbols: " + config.TradingWatchSymbols,
                Priority = targetPriority,
                Phase = ProjectPhase.Development,
                CompletionPercentage = 10,
                StartDate = DateTime.Now,
                Deadline = DateTime.Now.AddYears(1)
            }).ConfigureAwait(false);
        }
    }
}
