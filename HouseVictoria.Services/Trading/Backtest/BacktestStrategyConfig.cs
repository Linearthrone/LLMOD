using System.Text.Json;

namespace HouseVictoria.Services.Trading.Backtest
{
    /// <summary>
    /// Parsed strategy parameters for <see cref="BacktestEngine"/>.
    /// </summary>
    public sealed class BacktestStrategyConfig
    {
        public string StrategyType { get; set; } = "ma_crossover";
        public int FastPeriod { get; set; } = 10;
        public int SlowPeriod { get; set; } = 30;
        public int RsiPeriod { get; set; } = 14;
        public double RsiOversold { get; set; } = 30;
        public double RsiOverbought { get; set; } = 70;
        public int BreakoutPeriod { get; set; } = 20;
        public int MacdFast { get; set; } = 12;
        public int MacdSlow { get; set; } = 26;
        public int MacdSignal { get; set; } = 9;
        public int BollingerPeriod { get; set; } = 20;
        public double BollingerStdDev { get; set; } = 2.0;
        public double StopLossPips { get; set; }
        public double TakeProfitPips { get; set; }
        /// <summary>long, short, or both</summary>
        public string Direction { get; set; } = "both";

        public static BacktestStrategyConfig FromParameters(IReadOnlyDictionary<string, object>? parameters)
        {
            var config = new BacktestStrategyConfig();
            if (parameters == null || parameters.Count == 0)
                return config;

            config.StrategyType = GetString(parameters, "strategy_type", "strategyType", "type") ?? config.StrategyType;
            config.FastPeriod = GetInt(parameters, 10, "fast_period", "fastPeriod", "fast_ma");
            config.SlowPeriod = GetInt(parameters, 30, "slow_period", "slowPeriod", "slow_ma");
            config.RsiPeriod = GetInt(parameters, 14, "rsi_period", "rsiPeriod");
            config.RsiOversold = GetDouble(parameters, 30, "rsi_oversold", "rsiOversold", "oversold");
            config.RsiOverbought = GetDouble(parameters, 70, "rsi_overbought", "rsiOverbought", "overbought");
            config.BreakoutPeriod = GetInt(parameters, 20, "breakout_period", "breakoutPeriod", "lookback");
            config.MacdFast = GetInt(parameters, 12, "macd_fast", "macdFast");
            config.MacdSlow = GetInt(parameters, 26, "macd_slow", "macdSlow");
            config.MacdSignal = GetInt(parameters, 9, "macd_signal", "macdSignal", "signal_period");
            config.BollingerPeriod = GetInt(parameters, 20, "bollinger_period", "bollingerPeriod", "bb_period");
            config.BollingerStdDev = GetDouble(parameters, 2.0, "bollinger_std", "bollingerStdDev", "bb_std");
            config.StopLossPips = GetDouble(parameters, 0, "stop_loss_pips", "stopLossPips", "stop_pips");
            config.TakeProfitPips = GetDouble(parameters, 0, "take_profit_pips", "takeProfitPips", "tp_pips");
            config.Direction = GetString(parameters, "direction", "side") ?? config.Direction;

            return config;
        }

        private static string? GetString(IReadOnlyDictionary<string, object> dict, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!dict.TryGetValue(key, out var value) || value == null)
                    continue;

                if (value is JsonElement el && el.ValueKind == JsonValueKind.String)
                    return el.GetString();
                return value.ToString();
            }

            return null;
        }

        private static int GetInt(IReadOnlyDictionary<string, object> dict, int defaultValue, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!dict.TryGetValue(key, out var value) || value == null)
                    continue;

                if (value is JsonElement el)
                {
                    if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
                        return n;
                    if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var parsed))
                        return parsed;
                }
                else if (int.TryParse(value.ToString(), out var parsed))
                    return parsed;
            }

            return defaultValue;
        }

        private static double GetDouble(IReadOnlyDictionary<string, object> dict, double defaultValue, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!dict.TryGetValue(key, out var value) || value == null)
                    continue;

                if (value is JsonElement el)
                {
                    if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var n))
                        return n;
                    if (el.ValueKind == JsonValueKind.String &&
                        double.TryParse(el.GetString(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                        return parsed;
                }
                else if (double.TryParse(value.ToString(), System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }

            return defaultValue;
        }
    }
}
