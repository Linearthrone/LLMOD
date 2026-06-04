using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Trading
{
    /// <summary>
    /// Lightweight H1 technical signals (RSI / MACD / MA) for multi-pair watch — no LLM required.
    /// </summary>
    public static class TechnicalSignalScanner
    {
        public const int MinBars = 40;

        public static TechnicalSignalResult? Evaluate(string symbol, IReadOnlyList<HistoricalBar> bars)
        {
            if (bars.Count < MinBars)
                return null;

            var closes = bars.Select(b => b.Close).ToList();
            var last = bars.Count - 1;
            var prev = last - 1;

            var ma = TryMaCross(closes, last, prev);
            if (ma != null)
            {
                ma.Symbol = symbol;
                ma.TimeFrame = bars[0].TimeFrame;
                ma.BarTime = bars[last].Time;
                return ma;
            }

            var macd = TryMacdCross(closes, last, prev);
            if (macd != null)
            {
                macd.Symbol = symbol;
                macd.TimeFrame = bars[0].TimeFrame;
                macd.BarTime = bars[last].Time;
                return macd;
            }

            var rsi = TryRsiReversal(closes, last, prev);
            if (rsi != null)
            {
                rsi.Symbol = symbol;
                rsi.TimeFrame = bars[0].TimeFrame;
                rsi.BarTime = bars[last].Time;
                return rsi;
            }

            return null;
        }

        private static TechnicalSignalResult? TryMaCross(IReadOnlyList<double> closes, int last, int prev)
        {
            const int fast = 10;
            const int slow = 30;
            if (last < slow)
                return null;

            var fastNow = SimpleMa(closes, last, fast);
            var slowNow = SimpleMa(closes, last, slow);
            var fastPrev = SimpleMa(closes, prev, fast);
            var slowPrev = SimpleMa(closes, prev, slow);

            if (fastPrev <= slowPrev && fastNow > slowNow)
                return new TechnicalSignalResult
                {
                    SignalType = "ma_crossover",
                    Direction = "long",
                    SuggestedStrategy = "ma_crossover",
                    Message = $"MA({fast}/{slow}) bullish cross on latest bar"
                };

            if (fastPrev >= slowPrev && fastNow < slowNow)
                return new TechnicalSignalResult
                {
                    SignalType = "ma_crossover",
                    Direction = "short",
                    SuggestedStrategy = "ma_crossover",
                    Message = $"MA({fast}/{slow}) bearish cross on latest bar"
                };

            return null;
        }

        private static TechnicalSignalResult? TryMacdCross(IReadOnlyList<double> closes, int last, int prev)
        {
            const int fast = 12;
            const int slow = 26;
            const int signal = 9;
            if (last < slow + signal)
                return null;

            var macdNow = Ema(closes, last, fast) - Ema(closes, last, slow);
            var macdPrev = Ema(closes, prev, fast) - Ema(closes, prev, slow);
            var sigNow = MacdSignalLine(closes, last, fast, slow, signal);
            var sigPrev = MacdSignalLine(closes, prev, fast, slow, signal);

            if (macdPrev <= sigPrev && macdNow > sigNow)
                return new TechnicalSignalResult
                {
                    SignalType = "macd_crossover",
                    Direction = "long",
                    SuggestedStrategy = "macd",
                    Message = "MACD crossed above signal line"
                };

            if (macdPrev >= sigPrev && macdNow < sigNow)
                return new TechnicalSignalResult
                {
                    SignalType = "macd_crossover",
                    Direction = "short",
                    SuggestedStrategy = "macd",
                    Message = "MACD crossed below signal line"
                };

            return null;
        }

        private static TechnicalSignalResult? TryRsiReversal(IReadOnlyList<double> closes, int last, int prev)
        {
            const int period = 14;
            const double oversold = 30;
            const double overbought = 70;
            if (last < period)
                return null;

            var rsiNow = ComputeRsi(closes, last, period);
            var rsiPrev = ComputeRsi(closes, prev, period);

            if (rsiPrev <= oversold && rsiNow > oversold)
                return new TechnicalSignalResult
                {
                    SignalType = "rsi_reversal",
                    Direction = "long",
                    SuggestedStrategy = "rsi",
                    IndicatorValue = rsiNow,
                    Message = $"RSI({period}) crossed up through oversold ({rsiNow:F1})"
                };

            if (rsiPrev >= overbought && rsiNow < overbought)
                return new TechnicalSignalResult
                {
                    SignalType = "rsi_reversal",
                    Direction = "short",
                    SuggestedStrategy = "rsi",
                    IndicatorValue = rsiNow,
                    Message = $"RSI({period}) crossed down through overbought ({rsiNow:F1})"
                };

            return null;
        }

        private static double SimpleMa(IReadOnlyList<double> values, int endIndex, int period)
        {
            var sum = 0.0;
            for (var i = endIndex - period + 1; i <= endIndex; i++)
                sum += values[i];
            return sum / period;
        }

        private static double Ema(IReadOnlyList<double> values, int endIndex, int period)
        {
            if (endIndex < period - 1)
                return values[endIndex];

            var k = 2.0 / (period + 1);
            var ema = SimpleMa(values, period - 1, period);
            for (var i = period; i <= endIndex; i++)
                ema = values[i] * k + ema * (1 - k);
            return ema;
        }

        private static double MacdSignalLine(IReadOnlyList<double> closes, int endIndex, int fast, int slow, int signalPeriod)
        {
            var start = slow;
            if (endIndex < start)
                return 0;

            var k = 2.0 / (signalPeriod + 1);
            var seed = 0.0;
            var count = 0;
            for (var i = start; i < start + signalPeriod && i <= endIndex; i++)
            {
                seed += Ema(closes, i, fast) - Ema(closes, i, slow);
                count++;
            }

            seed = count > 0 ? seed / count : seed;
            var signal = seed;
            for (var i = start + signalPeriod; i <= endIndex; i++)
            {
                var macd = Ema(closes, i, fast) - Ema(closes, i, slow);
                signal = macd * k + signal * (1 - k);
            }

            return signal;
        }

        private static double ComputeRsi(IReadOnlyList<double> closes, int endIndex, int period)
        {
            double gains = 0;
            double losses = 0;
            for (var i = endIndex - period + 1; i <= endIndex; i++)
            {
                var delta = closes[i] - closes[i - 1];
                if (delta >= 0)
                    gains += delta;
                else
                    losses -= delta;
            }

            if (losses == 0)
                return 100;
            var rs = gains / losses;
            return 100 - 100 / (1 + rs);
        }
    }
}
