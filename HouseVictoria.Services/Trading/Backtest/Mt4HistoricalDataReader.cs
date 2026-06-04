using System.Globalization;
using System.Text.Json;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Trading.Backtest
{
    /// <summary>
    /// Reads MT4 .hst and bridge CSV historical files with broker symbol resolution.
    /// </summary>
    public static class Mt4HistoricalDataReader
    {
        public static async Task<List<HistoricalBar>> LoadBarsAsync(
            string mt4DataPath,
            string symbol,
            TimeFrame timeFrame,
            DateTime startDate,
            DateTime endDate,
            string commandFolder = "HouseVictoria")
        {
            var bars = new List<HistoricalBar>();
            var commandPath = Path.Combine(mt4DataPath, "MQL4", "Files", commandFolder);
            var symbolCandidates = ResolveSymbolCandidates(commandPath, symbol);

            var timeframeCode = GetTimeFrameCode(timeFrame);
            var historyPath = Path.Combine(mt4DataPath, "history");

            if (Directory.Exists(historyPath))
            {
                foreach (var brokerFolder in Directory.GetDirectories(historyPath))
                {
                    foreach (var candidate in symbolCandidates)
                    {
                        var symbolFolder = Path.Combine(brokerFolder, candidate);
                        var hstFile = Path.Combine(symbolFolder, $"{candidate}{timeframeCode}.hst");
                        if (File.Exists(hstFile))
                        {
                            bars = ReadHstFile(hstFile, symbol, timeFrame, startDate, endDate);
                            if (bars.Count > 0)
                                return bars;
                        }
                    }
                }
            }

            foreach (var candidate in symbolCandidates)
            {
                var csvPath = Path.Combine(commandPath, $"{candidate}_{timeframeCode}.csv");
                if (!File.Exists(csvPath))
                    csvPath = Path.Combine(commandPath, $"{symbol}_{timeframeCode}.csv");

                if (File.Exists(csvPath))
                {
                    bars = await ReadCsvAsync(csvPath, symbol, timeFrame, startDate, endDate).ConfigureAwait(false);
                    if (bars.Count > 0)
                        return bars;
                }
            }

            return bars;
        }

        public static List<string> ResolveSymbolCandidates(string commandPath, string symbol)
        {
            var baseSymbol = symbol.ToUpperInvariant();
            var candidates = new List<string> { baseSymbol };

            var mapFile = Path.Combine(commandPath, "SymbolMap.json");
            if (File.Exists(mapFile))
            {
                try
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(mapFile));
                    if (map != null)
                    {
                        foreach (var kvp in map)
                        {
                            if (kvp.Key.Equals(baseSymbol, StringComparison.OrdinalIgnoreCase) &&
                                !candidates.Contains(kvp.Value, StringComparer.OrdinalIgnoreCase))
                            {
                                candidates.Add(kvp.Value);
                            }
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            return candidates;
        }

        public static List<HistoricalBar> ReadHstFile(
            string filePath,
            string symbol,
            TimeFrame timeFrame,
            DateTime startDate,
            DateTime endDate)
        {
            var bars = new List<HistoricalBar>();

            try
            {
                using var file = File.OpenRead(filePath);
                using var reader = new BinaryReader(file);
                file.Seek(148, SeekOrigin.Begin);

                while (file.Position < file.Length)
                {
                    var unixSeconds = reader.ReadInt64();
                    var time = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                    var open = reader.ReadDouble();
                    var low = reader.ReadDouble();
                    var high = reader.ReadDouble();
                    var close = reader.ReadDouble();
                    var volume = reader.ReadInt64();
                    reader.ReadInt32();
                    reader.ReadInt32();

                    if (time >= startDate && time <= endDate)
                    {
                        bars.Add(new HistoricalBar
                        {
                            Time = time,
                            Open = open,
                            High = high,
                            Low = low,
                            Close = close,
                            Volume = volume,
                            Symbol = symbol,
                            TimeFrame = timeFrame
                        });
                    }

                    if (time > endDate)
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading .hst file {filePath}: {ex.Message}");
            }

            return bars.OrderBy(b => b.Time).ToList();
        }

        public static async Task<List<HistoricalBar>> ReadCsvAsync(
            string filePath,
            string symbol,
            TimeFrame timeFrame,
            DateTime startDate,
            DateTime endDate)
        {
            var bars = new List<HistoricalBar>();
            try
            {
                var lines = await File.ReadAllLinesAsync(filePath).ConfigureAwait(false);
                foreach (var line in lines.Skip(1))
                {
                    var parts = line.Split(',');
                    if (parts.Length < 6)
                        continue;

                    if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
                        continue;

                    if (time < startDate || time > endDate)
                        continue;

                    if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var open) ||
                        !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var high) ||
                        !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var low) ||
                        !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var close) ||
                        !long.TryParse(parts[5], out var volume))
                        continue;

                    bars.Add(new HistoricalBar
                    {
                        Time = time,
                        Open = open,
                        High = high,
                        Low = low,
                        Close = close,
                        Volume = volume,
                        Symbol = symbol,
                        TimeFrame = timeFrame
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading CSV {filePath}: {ex.Message}");
            }

            return bars.OrderBy(b => b.Time).ToList();
        }

        public static string GetTimeFrameCode(TimeFrame timeFrame) => timeFrame switch
        {
            TimeFrame.M1 => "1",
            TimeFrame.M5 => "5",
            TimeFrame.M15 => "15",
            TimeFrame.M30 => "30",
            TimeFrame.H1 => "60",
            TimeFrame.H4 => "240",
            TimeFrame.D1 => "1440",
            TimeFrame.W1 => "10080",
            TimeFrame.MN1 => "43200",
            _ => "60"
        };
    }
}
