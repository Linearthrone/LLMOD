using System.IO;
using System.Text.Json;
using HouseVictoria.Core.Models;
using HouseVictoria.Core.Utils;
using HouseVictoria.Services.RemoteCompanion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HouseVictoria.App.RemoteCompanion
{
    /// <summary>
    /// Minimal Kestrel host for phone → PC text and audio chat. TLS is expected at the edge (e.g. Cloudflare Tunnel, Tailscale).
    /// </summary>
    public sealed class RemoteCompanionWebHost : IAsyncDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private WebApplication? _app;

        public async Task StartIfEnabledAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            var cfg = serviceProvider.GetRequiredService<AppConfig>();
            if (!cfg.RemoteCompanionEnabled)
                return;

            if (string.IsNullOrWhiteSpace(cfg.RemoteCompanionApiToken) || cfg.RemoteCompanionApiToken.Length < 16)
            {
                LoggingHelper.WriteToStartupLog(
                    "Remote companion: not started — set RemoteCompanionApiToken to at least 16 characters in Settings or App.config.");
                return;
            }

            if (cfg.RemoteCompanionListenPort is < 1 or > 65535)
            {
                LoggingHelper.WriteToStartupLog("Remote companion: invalid RemoteCompanionListenPort.");
                return;
            }

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddCors();
            builder.Services.AddSingleton(_ => serviceProvider.GetRequiredService<RemoteCompanionChatService>());
            builder.Services.AddSingleton(_ => cfg);

            var bindHost = cfg.RemoteCompanionListenOnLan ? "0.0.0.0" : "127.0.0.1";
            builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://{bindHost}:{cfg.RemoteCompanionListenPort}");

            var app = builder.Build();
            app.UseCors(static p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

            app.MapGet("/api/remote/v1/health", () =>
                Results.Json(new { ok = true, service = "house-victoria-remote", version = 1 }));

            app.MapPost("/api/remote/v1/chat", async (HttpContext http, RemoteCompanionChatService chatService, CancellationToken ct) =>
            {
                if (!IsAuthorized(http, cfg))
                    return Results.Json(new { error = "unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

                RemoteChatRequest? body;
                try
                {
                    body = await JsonSerializer.DeserializeAsync<RemoteChatRequest>(http.Request.Body, JsonOptions, ct)
                        .ConfigureAwait(false);
                }
                catch
                {
                    return Results.Json(new { error = "invalid_json" }, statusCode: StatusCodes.Status400BadRequest);
                }

                if (body == null || string.IsNullOrWhiteSpace(body.Message))
                    return Results.Json(new { error = "message_required" }, statusCode: StatusCodes.Status400BadRequest);

                var result = await chatService.ChatAsync(body.Message, body.ContactId, ct).ConfigureAwait(false);
                if (!result.IsSuccess)
                    return Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status400BadRequest);

                return Results.Json(new { reply = result.Reply, conversationId = result.ConversationId });
            });

            app.MapPost("/api/remote/v1/chat-audio", async (HttpContext http, RemoteCompanionChatService chatService, CancellationToken ct) =>
            {
                if (!IsAuthorized(http, cfg))
                    return Results.Json(new { error = "unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

                if (!http.Request.HasFormContentType)
                {
                    return Results.Json(
                        new { error = "multipart_form_required", detail = "Use multipart/form-data with field 'audio' (and optional 'contactId')." },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var form = await http.Request.ReadFormAsync(ct).ConfigureAwait(false);
                var file = form.Files.GetFile("audio");
                if (file == null || file.Length == 0)
                    return Results.Json(new { error = "audio_field_required" }, statusCode: StatusCodes.Status400BadRequest);

                await using var stream = file.OpenReadStream();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
                var bytes = ms.ToArray();

                var contactId = form.TryGetValue("contactId", out var cid) ? cid.ToString() : null;
                var result = await chatService.ChatFromAudioAsync(bytes, string.IsNullOrWhiteSpace(contactId) ? null : contactId, ct)
                    .ConfigureAwait(false);
                if (!result.IsSuccess)
                    return Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status400BadRequest);

                return Results.Json(new { reply = result.Reply, conversationId = result.ConversationId });
            });

            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            _app = app;

            var bind = cfg.RemoteCompanionListenOnLan ? "all interfaces" : "127.0.0.1";
            LoggingHelper.WriteToStartupLog(
                $"Remote companion API listening on http://{bind}:{cfg.RemoteCompanionListenPort} (text: POST /api/remote/v1/chat, audio: POST /api/remote/v1/chat-audio).");
        }

        private static bool IsAuthorized(HttpContext http, AppConfig cfg)
        {
            if (http.Request.Headers.TryGetValue("X-Api-Key", out var apiKey))
            {
                if (string.Equals(apiKey.ToString(), cfg.RemoteCompanionApiToken, StringComparison.Ordinal))
                    return true;
            }

            var auth = http.Request.Headers.Authorization.ToString();
            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = auth["Bearer ".Length..].Trim();
                if (string.Equals(token, cfg.RemoteCompanionApiToken, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public async ValueTask DisposeAsync()
        {
            if (_app != null)
            {
                try
                {
                    await _app.StopAsync().ConfigureAwait(false);
                }
                catch { /* ignore */ }

                await _app.DisposeAsync().ConfigureAwait(false);
                _app = null;
            }
        }

        private sealed class RemoteChatRequest
        {
            public string? Message { get; set; }
            public string? ContactId { get; set; }
        }
    }
}
