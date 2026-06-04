using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Trading.Backtest
{
    /// <summary>
    /// Bar-by-bar backtest simulator with pluggable strategy types from <see cref="BacktestStrategyConfig"/>.
    /// </summary>
    public static class BacktestEngine
    {
        public static BacktestResult Run(IReadOnlyList<HistoricalBar> bars, BacktestRequest request)
        {
            if (bars.Count == 0)
            {
                return new BacktestResult
                {
                    Success = false,
                    ErrorMessage = "No historical bars to backtest"
                };
            }

            var config = BacktestStrategyConfig.FromParameters(request.StrategyParameters);
            var pipSize = GetPipSize(request.Symbol);

            var result = new BacktestResult
            {
                InitialDeposit = request.InitialDeposit,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Trades = new List<Trade>(),
                EquityCurve = new List<EquityPoint>()
            };

            double balance = request.InitialDeposit;
            double equity = balance;
            double maxEquity = balance;
            double maxDrawdown = 0;
            double totalProfit = 0;
            double totalLoss = 0;
            var trades = new List<Trade>();
            Trade? openTrade = null;

            var closes = bars.Select(b => b.Close).ToList();
            var allowsLong = config.Direction is not "short";
            var allowsShort = config.Direction is not "long";

            for (var i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                var signal = EvaluateSignal(config, bars, closes, i);

                if (openTrade != null)
                {
                    var hitSl = false;
                    var hitTp = false;
                    if (config.StopLossPips > 0 || config.TakeProfitPips > 0)
                    {
                        if (openTrade.Type == TradeType.Buy)
                        {
                            if (config.StopLossPips > 0 && bar.Low <= openTrade.OpenPrice - config.StopLossPips * pipSize)
                                hitSl = true;
                            if (config.TakeProfitPips > 0 && bar.High >= openTrade.OpenPrice + config.TakeProfitPips * pipSize)
                                hitTp = true;
                        }
                        else
                        {
                            if (config.StopLossPips > 0 && bar.High >= openTrade.OpenPrice + config.StopLossPips * pipSize)
                                hitSl = true;
                            if (config.TakeProfitPips > 0 && bar.Low <= openTrade.OpenPrice - config.TakeProfitPips * pipSize)
                                hitTp = true;
                        }
                    }

                    var closeOnOpposite = (openTrade.Type == TradeType.Buy && signal == Signal.EnterShort) ||
                                          (openTrade.Type == TradeType.Sell && signal == Signal.EnterLong);
                    var closeOnSignal = (openTrade.Type == TradeType.Buy && signal == Signal.ExitLong) ||
                                        (openTrade.Type == TradeType.Sell && signal == Signal.ExitShort);

                    if (hitSl || hitTp || closeOnSignal || closeOnOpposite)
                    {
                        var exitPrice = bar.Close;
                        if (hitSl && config.StopLossPips > 0)
                        {
                            exitPrice = openTrade.Type == TradeType.Buy
                                ? openTrade.OpenPrice - config.StopLossPips * pipSize
                                : openTrade.OpenPrice + config.StopLossPips * pipSize;
                        }
                        else if (hitTp && config.TakeProfitPips > 0)
                        {
                            exitPrice = openTrade.Type == TradeType.Buy
                                ? openTrade.OpenPrice + config.TakeProfitPips * pipSize
                                : openTrade.OpenPrice - config.TakeProfitPips * pipSize;
                        }

                        CloseTrade(openTrade, exitPrice, bar.Time, request.LotSize, ref balance, ref totalProfit, ref totalLoss);
                        trades.Add(openTrade);
                        openTrade = null;
                    }
                }

                if (openTrade == null)
                {
                    if (allowsLong && signal == Signal.EnterLong)
                    {
                        openTrade = OpenTrade(trades.Count + 1, request.Symbol, TradeType.Buy, request.LotSize, bar);
                    }
                    else if (allowsShort && signal == Signal.EnterShort)
                    {
                        openTrade = OpenTrade(trades.Count + 1, request.Symbol, TradeType.Sell, request.LotSize, bar);
                    }
                }

                equity = balance;
                if (openTrade != null)
                {
                    equity += UnrealizedPnl(openTrade, bar.Close, request.LotSize);
                }

                if (equity > maxEquity)
                    maxEquity = equity;

                var drawdown = maxEquity - equity;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;

                result.EquityCurve.Add(new EquityPoint
                {
                    Time = bar.Time,
                    Equity = equity,
                    Balance = balance
                });
            }

            if (openTrade != null)
            {
                var last = bars[^1];
                CloseTrade(openTrade, last.Close, last.Time, request.LotSize, ref balance, ref totalProfit, ref totalLoss);
                trades.Add(openTrade);
            }

            result.FinalBalance = balance;
            result.NetProfit = balance - request.InitialDeposit;
            result.ProfitPercent = request.InitialDeposit > 0 ? (result.NetProfit / request.InitialDeposit) * 100 : 0;
            result.TotalTrades = trades.Count;
            result.WinningTrades = trades.Count(t => t.Profit > 0);
            result.LosingTrades = trades.Count(t => t.Profit < 0);
            result.WinRate = result.TotalTrades > 0 ? (double)result.WinningTrades / result.TotalTrades * 100 : 0;
            result.MaxDrawdown = maxDrawdown;
            result.MaxDrawdownPercent = maxEquity > 0 ? (maxDrawdown / maxEquity) * 100 : 0;
            result.ProfitFactor = Math.Abs(totalLoss) > 0 ? totalProfit / Math.Abs(totalLoss) : 0;
            result.Trades = trades;
            result.Success = true;
            result.BarsProcessed = bars.Count;
            result.StrategyTypeUsed = config.StrategyType;
            result.SharpeRatio = ComputeSharpeRatio(result.EquityCurve);

            return result;
        }

        private enum Signal
        {
            None,
            EnterLong,
            EnterShort,
            ExitLong,
            ExitShort
        }

        private static Signal EvaluateSignal(
            BacktestStrategyConfig config,
            IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<double> closes,
            int index)
        {
            return config.StrategyType.ToLowerInvariant() switch
            {
                "rsi" or "rsi_reversal" or "rsi_mean_reversion" =>
                    EvaluateRsiSignal(config, closes, index),
                "breakout" or "donchian" =>
                    EvaluateBreakoutSignal(config, bars, index),
                "macd" or "macd_crossover" =>
                    EvaluateMacdSignal(config, closes, index),
                "bollinger" or "bollinger_mean_reversion" or "bb_mean_reversion" =>
                    EvaluateBollingerSignal(config, closes, index),
                "ema_crossover" or "ema" =>
                    EvaluateEmaCrossSignal(config, closes, index),
                _ => EvaluateMaCrossSignal(config, closes, index)
            };
        }

        private static Signal EvaluateMaCrossSignal(BacktestStrategyConfig config, IReadOnlyList<double> closes, int index)
        {
            if (index < config.SlowPeriod)
                return Signal.None;

            var fastNow = SimpleMa(closes, index, config.FastPeriod);
            var slowNow = SimpleMa(closes, index, config.SlowPeriod);
            var fastPrev = SimpleMa(closes, index - 1, config.FastPeriod);
            var slowPrev = SimpleMa(closes, index - 1, config.SlowPeriod);

            if (fastPrev <= slowPrev && fastNow > slowNow)
                return Signal.EnterLong;
            if (fastPrev >= slowPrev && fastNow < slowNow)
                return Signal.EnterShort;

            return Signal.None;
        }

        private static Signal EvaluateEmaCrossSignal(BacktestStrategyConfig config, IReadOnlyList<double> closes, int index)
        {
            if (index < config.SlowPeriod)
                return Signal.None;

            var fastNow = ExponentialMa(closes, index, config.FastPeriod);
            var slowNow = ExponentialMa(closes, index, config.SlowPeriod);
            var fastPrev = ExponentialMa(closes, index - 1, config.FastPeriod);
            var slowPrev = ExponentialMa(closes, index - 1, config.SlowPeriod);

            if (fastPrev <= slowPrev && fastNow > slowNow)
                return Signal.EnterLong;
            if (fastPrev >= slowPrev && fastNow < slowNow)
                return Signal.EnterShort;

            return Signal.None;
        }

        private static Signal EvaluateMacdSignal(BacktestStrategyConfig config, IReadOnlyList<double> closes, int index)
        {
            var minBars = config.MacdSlow + config.MacdSignal;
            if (index < minBars)
                return Signal.None;

            var macdNow = ExponentialMa(closes, index, config.MacdFast) - ExponentialMa(closes, index, config.MacdSlow);
            var macdPrev = ExponentialMa(closes, index - 1, config.MacdFast) - ExponentialMa(closes, index - 1, config.MacdSlow);
            var signalNow = MacdSignalLine(closes, index, config);
            var signalPrev = MacdSignalLine(closes, index - 1, config);

            if (macdPrev <= signalPrev && macdNow > signalNow)
                return Signal.EnterLong;
            if (macdPrev >= signalPrev && macdNow < signalNow)
                return Signal.EnterShort;

            return Signal.None;
        }

        private static double MacdSignalLine(IReadOnlyList<double> closes, int endIndex, BacktestStrategyConfig config)
        {
            var start = config.MacdSlow;
            if (endIndex < start)
                return 0;

            var k = 2.0 / (config.MacdSignal + 1);
            var seed = 0.0;
            for (var i = start; i < start + config.MacdSignal && i <= endIndex; i++)
                seed += ExponentialMa(closes, i, config.MacdFast) - ExponentialMa(closes, i, config.MacdSlow);
            seed /= config.MacdSignal;

            var signal = seed;
            for (var i = start + config.MacdSignal; i <= endIndex; i++)
            {
                var macd = ExponentialMa(closes, i, config.MacdFast) - ExponentialMa(closes, i, config.MacdSlow);
                signal = macd * k + signal * (1 - k);
            }

            return signal;
        }

        private static Signal EvaluateBollingerSignal(BacktestStrategyConfig config, IReadOnlyList<double> closes, int index)
        {
            if (index < config.BollingerPeriod)
                return Signal.None;

            var middle = SimpleMa(closes, index, config.BollingerPeriod);
            var std = StdDev(closes, index, config.BollingerPeriod);
            var upper = middle + config.BollingerStdDev * std;
            var lower = middle - config.BollingerStdDev * std;
            var price = closes[index];
            var prev = closes[index - 1];

            if (prev >= lower && price < lower)
                return Signal.EnterLong;
            if (prev <= upper && price > upper)
                return Signal.EnterShort;

            return Signal.None;
        }

        private static Signal EvaluateRsiSignal(BacktestStrategyConfig config, IReadOnlyList<double> closes, int index)
        {
            if (index < config.RsiPeriod)
                return Signal.None;

            var rsiNow = ComputeRsi(closes, index, config.RsiPeriod);
            var rsiPrev = ComputeRsi(closes, index - 1, config.RsiPeriod);

            if (rsiPrev <= config.RsiOversold && rsiNow > config.RsiOversold)
                return Signal.EnterLong;
            if (rsiPrev >= config.RsiOverbought && rsiNow < config.RsiOverbought)
                return Signal.EnterShort;
            if (rsiNow >= config.RsiOverbought)
                return Signal.ExitLong;
            if (rsiNow <= config.RsiOversold)
                return Signal.ExitShort;

            return Signal.None;
        }

        private static Signal EvaluateBreakoutSignal(BacktestStrategyConfig config, IReadOnlyList<HistoricalBar> bars, int index)
        {
            if (index < config.BreakoutPeriod)
                return Signal.None;

            var window = bars.Skip(index - config.BreakoutPeriod).Take(config.BreakoutPeriod).ToList();
            var highest = window.Max(b => b.High);
            var lowest = window.Min(b => b.Low);
            var bar = bars[index];

            if (bar.Close > highest)
                return Signal.EnterLong;
            if (bar.Close < lowest)
                return Signal.EnterShort;

            return Signal.None;
        }

        private static double SimpleMa(IReadOnlyList<double> values, int endIndex, int period)
        {
            var sum = 0.0;
            for (var i = endIndex - period + 1; i <= endIndex; i++)
                sum += values[i];
            return sum / period;
        }

        private static double ExponentialMa(IReadOnlyList<double> values, int endIndex, int period)
        {
            if (endIndex < period - 1)
                return values[endIndex];

            var k = 2.0 / (period + 1);
            var ema = SimpleMa(values, period - 1, period);
            for (var i = period; i <= endIndex; i++)
                ema = values[i] * k + ema * (1 - k);
            return ema;
        }

        private static double StdDev(IReadOnlyList<double> values, int endIndex, int period)
        {
            var mean = SimpleMa(values, endIndex, period);
            var sumSq = 0.0;
            for (var i = endIndex - period + 1; i <= endIndex; i++)
            {
                var d = values[i] - mean;
                sumSq += d * d;
            }

            return Math.Sqrt(sumSq / period);
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

        private static Trade OpenTrade(int ticket, string symbol, TradeType type, double lotSize, HistoricalBar bar) =>
            new()
            {
                Ticket = ticket,
                Symbol = symbol,
                Type = type,
                Volume = lotSize,
                OpenPrice = bar.Close,
                OpenTime = bar.Time
            };

        private static void CloseTrade(
            Trade trade,
            double exitPrice,
            DateTime exitTime,
            double lotSize,
            ref double balance,
            ref double totalProfit,
            ref double totalLoss)
        {
            trade.ClosePrice = exitPrice;
            trade.CloseTime = exitTime;
            trade.Profit = trade.Type == TradeType.Buy
                ? (exitPrice - trade.OpenPrice) * lotSize * 100000
                : (trade.OpenPrice - exitPrice) * lotSize * 100000;
            balance += trade.Profit ?? 0;
            var profit = trade.Profit ?? 0;
            if (profit > 0)
                totalProfit += profit;
            else
                totalLoss += profit;
        }

        private static double UnrealizedPnl(Trade trade, double price, double lotSize) =>
            trade.Type == TradeType.Buy
                ? (price - trade.OpenPrice) * lotSize * 100000
                : (trade.OpenPrice - price) * lotSize * 100000;

        private static double GetPipSize(string symbol) =>
            symbol.Contains("JPY", StringComparison.OrdinalIgnoreCase) ? 0.01 : 0.0001;

        private static double ComputeSharpeRatio(IReadOnlyList<EquityPoint> curve)
        {
            if (curve.Count < 2)
                return 0;

            var returns = new List<double>();
            for (var i = 1; i < curve.Count; i++)
            {
                var prev = curve[i - 1].Equity;
                if (prev > 0)
                    returns.Add((curve[i].Equity - prev) / prev);
            }

            if (returns.Count == 0)
                return 0;

            var avg = returns.Average();
            var std = Math.Sqrt(returns.Select(r => Math.Pow(r - avg, 2)).Sum() / returns.Count);
            return std > 0 ? avg / std * Math.Sqrt(252) : 0;
        }
    }
}
