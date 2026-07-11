using System.Runtime.CompilerServices;
using System.Text.Json;

namespace HouseVictoria.Services.AIServices
{
    internal enum HermesSseEventKind
    {
        ContentDelta,
        ToolProgress,
        Done,
        KeepAlive
    }

    internal readonly struct HermesSseEvent
    {
        public HermesSseEventKind Kind { get; init; }
        public string? Text { get; init; }
        public string? ToolName { get; init; }
        public string? ToolLabel { get; init; }
        public string? ToolStatus { get; init; }
    }

    /// <summary>Parses Hermes /v1/chat/completions SSE (OpenAI chunks + hermes.tool.progress).</summary>
    internal static class HermesChatSseReader
    {
        public static async IAsyncEnumerable<HermesSseEvent> ReadEventsAsync(
            StreamReader reader,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string? pendingEvent = null;

            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null)
                    break;

                if (line.Length == 0)
                {
                    pendingEvent = null;
                    continue;
                }

                if (line.StartsWith(':'))
                {
                    yield return new HermesSseEvent { Kind = HermesSseEventKind.KeepAlive };
                    continue;
                }

                if (line.StartsWith("event:", StringComparison.Ordinal))
                {
                    pendingEvent = line["event:".Length..].Trim();
                    continue;
                }

                if (!line.StartsWith("data:", StringComparison.Ordinal))
                    continue;

                var data = line["data:".Length..].Trim();
                if (data == "[DONE]")
                {
                    yield return new HermesSseEvent { Kind = HermesSseEventKind.Done };
                    yield break;
                }

                if (string.Equals(pendingEvent, "hermes.tool.progress", StringComparison.Ordinal))
                {
                    foreach (var toolEvt in ParseToolProgress(data))
                        yield return toolEvt;
                    continue;
                }

                foreach (var contentEvt in ParseContentChunk(data))
                    yield return contentEvt;
            }
        }

        private static IEnumerable<HermesSseEvent> ParseToolProgress(string json)
        {
            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var tool = root.TryGetProperty("tool", out var t) ? t.GetString() : null;
                var label = root.TryGetProperty("label", out var l) ? l.GetString() : null;
                var status = root.TryGetProperty("status", out var s) ? s.GetString() : "running";
                if (string.IsNullOrWhiteSpace(tool))
                    return Array.Empty<HermesSseEvent>();

                return new[]
                {
                    new HermesSseEvent
                    {
                        Kind = HermesSseEventKind.ToolProgress,
                        ToolName = tool,
                        ToolLabel = label ?? tool,
                        ToolStatus = status ?? "running"
                    }
                };
            }
            catch
            {
                return Array.Empty<HermesSseEvent>();
            }
            finally
            {
                doc?.Dispose();
            }
        }

        private static IEnumerable<HermesSseEvent> ParseContentChunk(string json)
        {
            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
                    return Array.Empty<HermesSseEvent>();

                var events = new List<HermesSseEvent>();
                foreach (var choice in choices.EnumerateArray())
                {
                    if (!choice.TryGetProperty("delta", out var delta))
                        continue;
                    if (!delta.TryGetProperty("content", out var contentProp))
                        continue;

                    var text = contentProp.GetString();
                    if (string.IsNullOrEmpty(text))
                        continue;

                    events.Add(new HermesSseEvent
                    {
                        Kind = HermesSseEventKind.ContentDelta,
                        Text = text
                    });
                }

                return events;
            }
            catch
            {
                return Array.Empty<HermesSseEvent>();
            }
            finally
            {
                doc?.Dispose();
            }
        }
    }
}
