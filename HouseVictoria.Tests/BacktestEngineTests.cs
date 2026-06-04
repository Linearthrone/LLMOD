using HouseVictoria.Core.Models;
using HouseVictoria.Services.Trading.Backtest;
using Xunit;

namespace HouseVictoria.Tests
{
    public class BacktestEngineTests
    {
        [Fact]
        public void Run_MaCrossover_ProducesTradesOnTrendingSeries()
        {
            var bars = BuildOscillatingBars(120, startPrice: 1.10, amplitude: 0.01);
            var request = new BacktestRequest
            {
                StrategyName = "TestMA",
                Symbol = "EURUSD",
                TimeFrame = TimeFrame.H1,
                StartDate = bars[0].Time,
                EndDate = bars[^1].Time,
                InitialDeposit = 10000,
                LotSize = 0.01,
                StrategyParameters = new Dictionary<string, object>
                {
                    ["strategy_type"] = "ma_crossover",
                    ["fast_period"] = 5,
                    ["slow_period"] = 15
                }
            };

            var result = BacktestEngine.Run(bars, request);

            Assert.True(result.Success);
            Assert.True(result.TotalTrades > 0);
            Assert.True(result.BarsProcessed == bars.Count);
        }

        [Fact]
        public void Run_BollingerMeanReversion_ProducesTradesOnOscillatingSeries()
        {
            var bars = BuildOscillatingBars(150, startPrice: 1.10, amplitude: 0.02);
            var request = new BacktestRequest
            {
                StrategyName = "TestBB",
                Symbol = "EURUSD",
                TimeFrame = TimeFrame.H1,
                StartDate = bars[0].Time,
                EndDate = bars[^1].Time,
                InitialDeposit = 10000,
                LotSize = 0.01,
                StrategyParameters = new Dictionary<string, object>
                {
                    ["strategy_type"] = "bollinger_mean_reversion",
                    ["bollinger_period"] = 10,
                    ["bollinger_std"] = 1.5
                }
            };

            var result = BacktestEngine.Run(bars, request);

            Assert.True(result.Success);
            Assert.True(result.TotalTrades > 0);
        }

        [Fact]
        public void TryParseBacktestRequest_ParsesFencedBlock()
        {
            var text = """
                ```backtest
                {"strategy_name":"Scalp","symbol":"EURUSD","time_frame":"H1","strategy_type":"rsi","start_date":"2025-06-01","end_date":"2026-01-01"}
                ```
                """;

            var request = HouseVictoria.Services.Trading.Mt4TradeBridgeHelper.TryParseBacktestRequest(text);

            Assert.NotNull(request);
            Assert.Equal("EURUSD", request!.Symbol);
            Assert.Equal("rsi", request.StrategyParameters["strategy_type"]?.ToString());
        }

        private static List<HistoricalBar> BuildOscillatingBars(int count, double startPrice, double amplitude)
        {
            var bars = new List<HistoricalBar>();
            var time = DateTime.UtcNow.AddHours(-count);
            for (var i = 0; i < count; i++)
            {
                var wave = Math.Sin(i * 0.25) * amplitude;
                var price = startPrice + wave;
                bars.Add(new HistoricalBar
                {
                    Time = time.AddHours(i),
                    Open = price,
                    High = price + 0.0005,
                    Low = price - 0.0005,
                    Close = price,
                    Volume = 100,
                    Symbol = "EURUSD",
                    TimeFrame = TimeFrame.H1
                });
            }

            return bars;
        }
    }
}
