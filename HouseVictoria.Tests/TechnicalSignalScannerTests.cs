using HouseVictoria.Core.Models;
using HouseVictoria.Services.Trading;
using Xunit;

namespace HouseVictoria.Tests
{
    public class TechnicalSignalScannerTests
    {
        [Fact]
        public void Evaluate_ReturnsSignal_OnOscillatingSeries()
        {
            var bars = BuildOscillatingBars(120, startPrice: 1.1, amplitude: 0.015);
            var result = TechnicalSignalScanner.Evaluate("EURUSD", bars);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result!.Direction));
            Assert.False(string.IsNullOrWhiteSpace(result.SuggestedStrategy));
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
