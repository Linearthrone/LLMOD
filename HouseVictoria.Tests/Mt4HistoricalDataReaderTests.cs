using HouseVictoria.Core.Models;
using HouseVictoria.Services.Trading.Backtest;
using Xunit;

namespace HouseVictoria.Tests
{
    public class Mt4HistoricalDataReaderTests
    {
        [Fact]
        public void ResolveHstCandidates_IncludesFlatBrokerFolderLayout()
        {
            var paths = Mt4HistoricalDataReader.ResolveHstCandidates(
                @"C:\history\Forex.com-Demo 108", "EURUSD", "60").ToList();

            Assert.Contains(@"C:\history\Forex.com-Demo 108\EURUSD60.hst", paths);
            Assert.Contains(@"C:\history\Forex.com-Demo 108\EURUSD\EURUSD60.hst", paths);
        }

        [Fact]
        public async Task LoadBarsAsync_ReadsLocalTerminalHistory_WhenPresent()
        {
            var terminalRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MetaQuotes", "Terminal");

            if (!Directory.Exists(terminalRoot))
                return;

            string? mt4Path = null;
            foreach (var dir in Directory.GetDirectories(terminalRoot))
            {
                var hst = Path.Combine(dir, "history", "Forex.com-Demo 108", "EURUSD60.hst");
                if (File.Exists(hst))
                {
                    mt4Path = dir;
                    break;
                }
            }

            if (mt4Path == null)
                return;

            var end = DateTime.UtcNow;
            var start = end.AddDays(-30);
            var bars = await Mt4HistoricalDataReader.LoadBarsAsync(
                mt4Path, "EURUSD", TimeFrame.H1, start, end);

            Assert.NotEmpty(bars);
        }

        [Fact]
        public async Task MetaTrader4Service_RunBacktest_EurUsdH1_SucceedsWithLocalHistory()
        {
            var terminalRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MetaQuotes", "Terminal");

            string? mt4Path = null;
            foreach (var dir in Directory.GetDirectories(terminalRoot))
            {
                var hst = Path.Combine(dir, "history", "Forex.com-Demo 108", "EURUSD60.hst");
                if (File.Exists(hst))
                {
                    mt4Path = dir;
                    break;
                }
            }

            if (mt4Path == null)
                return;

            var service = new HouseVictoria.Services.Trading.MetaTrader4Service();
            var connected = await service.ConnectAsync(mt4Path);
            Assert.True(connected);

            var end = DateTime.UtcNow;
            var start = end.AddDays(-30);
            var result = await service.RunBacktestAsync(new BacktestRequest
            {
                StrategyName = "PostRestartVerify",
                Symbol = "EURUSD",
                TimeFrame = TimeFrame.H1,
                StartDate = start,
                EndDate = end,
                InitialDeposit = 10000,
                LotSize = 0.01,
                StrategyParameters = new Dictionary<string, object>
                {
                    ["strategy_type"] = "ma_crossover",
                    ["fast_period"] = 10,
                    ["slow_period"] = 30
                }
            });

            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.BarsProcessed > 100);
            Assert.True(result.TotalTrades >= 0);
        }
    }
}
