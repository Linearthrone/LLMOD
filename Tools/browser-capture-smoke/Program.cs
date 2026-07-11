using System.Net.Http;
using System.Text.Json;
using System.Drawing;
using System.Drawing.Imaging;

const string baseUrl = "http://127.0.0.1:17891";
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

Console.WriteLine("=== Browser capture smoke (same path as HV) ===");

var health = await http.GetStringAsync($"{baseUrl}/health");
Console.WriteLine($"health: {health}");

await http.PostAsync($"{baseUrl}/stream/enable",
    new StringContent("{\"enabled\":true}", System.Text.Encoding.UTF8, "application/json"));

var latestJson = await http.GetStringAsync($"{baseUrl}/latest");
using var latestDoc = JsonDocument.Parse(latestJson);
var root = latestDoc.RootElement;
var ok = root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
var capturedAt = root.TryGetProperty("captured_at", out var ca) ? ca.GetDouble() : 0;
var b64 = root.TryGetProperty("screenshot_base64", out var b) ? b.GetString() : null;
var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - capturedAt;
Console.WriteLine($"latest ok={ok} title={title} age_sec={age:F2} b64_len={b64?.Length ?? 0}");

var dto = JsonSerializer.Deserialize<LatestDto>(latestJson, jsonOpts);
Console.WriteLine($"dto deserialize ok={dto?.Ok} b64_len={dto?.ScreenshotBase64?.Length ?? 0}");

sealed class LatestDto
{
    public bool Ok { get; set; }
    public string? ScreenshotBase64 { get; set; }
}

if (string.IsNullOrEmpty(b64))
{
    Console.WriteLine("FAIL: no base64 in /latest");
    return 1;
}

byte[] png;
try
{
    png = Convert.FromBase64String(b64);
    Console.WriteLine($"png bytes={png.Length}");
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL base64 decode: {ex.Message}");
    return 1;
}

try
{
    using var ms = new MemoryStream(png);
    using var bitmap = new Bitmap(ms);
    Console.WriteLine($"bitmap decode OK: {bitmap.Width}x{bitmap.Height}");
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL bitmap decode: {ex.Message}");
    return 1;
}

var captureBody = "{\"include_screenshot\":true,\"include_page_map\":false,\"timeout_seconds\":8}";
var capResp = await http.PostAsync($"{baseUrl}/capture",
    new StringContent(captureBody, System.Text.Encoding.UTF8, "application/json"));
var capJson = await capResp.Content.ReadAsStringAsync();
Console.WriteLine($"on-demand capture: {capJson[..Math.Min(200, capJson.Length)]}");

return 0;
