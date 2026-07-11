using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Trading
{
    /// <summary>
    /// Routes market-watch alerts into trade blocks, backtests, and project note logging.
    /// Keeps the scanner ignorant of execution so it can run even when the bridge is offline.
    /// </summary>
    public interface IOpportunityRouter
    {
        void RouteAlert(MarketWatchAlert alert);
        void RouteSignal(TechnicalSignalResult signal, MarketData? quote);
    }

    public sealed class NoOpportunityRouter : IOpportunityRouter
    {
        public void RouteAlert(MarketWatchAlert alert)
        {
            // Reserved for future wiring to trade/backtest blocks.
        }

        public void RouteSignal(TechnicalSignalResult signal, MarketData? quote)
        {
            // Reserved for future wiring to trade/backtest blocks.
        }
    }
}
