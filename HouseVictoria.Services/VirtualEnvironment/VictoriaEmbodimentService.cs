using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.VirtualEnvironment
{
    /// <summary>
    /// Connects Victoria's chat cognition to the in-scene MetaHuman in Unreal (walk, talk, see, touch).
    /// </summary>
    public sealed class VictoriaEmbodimentService : IVictoriaEmbodimentService
    {
        private readonly IVirtualEnvironmentService _virtualEnvironment;
        private readonly AppConfig _appConfig;
        private readonly IPersistenceService? _persistence;
        private bool _startupComplete;

        public VictoriaEmbodimentService(
            IVirtualEnvironmentService virtualEnvironment,
            AppConfig appConfig,
            IPersistenceService? persistence = null)
        {
            _virtualEnvironment = virtualEnvironment ?? throw new ArgumentNullException(nameof(virtualEnvironment));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _persistence = persistence;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (!_appConfig.EnableVictoriaEmbodiment)
                return;

            if (!await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false))
            {
                System.Diagnostics.Debug.WriteLine("VictoriaEmbodiment: Unreal not reachable at startup (PIE or bridge not running).");
                return;
            }

            var avatarId = AvatarId;
            try
            {
                await _virtualEnvironment.FocusAvatarAsync(avatarId).ConfigureAwait(false);
                await _virtualEnvironment.SetLocomotionAsync(avatarId, _appConfig.WalkSpeed, _appConfig.RunSpeed).ConfigureAwait(false);
                await _virtualEnvironment.SendCommandAsync("status").ConfigureAwait(false);
                _startupComplete = true;
                System.Diagnostics.Debug.WriteLine($"VictoriaEmbodiment: focused '{avatarId}' on Unreal bridge.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VictoriaEmbodiment: startup handshake failed: {ex.Message}");
            }
        }

        public async Task OnChatExchangeAsync(
            string contactId,
            string userMessage,
            string assistantMessage,
            CancellationToken cancellationToken = default)
        {
            if (!_appConfig.EnableVictoriaEmbodiment)
                return;

            if (!await ShouldEmbodyContactAsync(contactId, cancellationToken).ConfigureAwait(false))
                return;

            if (!await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false))
                return;

            if (!_startupComplete)
                await StartAsync(cancellationToken).ConfigureAwait(false);

            var avatarId = AvatarId;
            var intents = VictoriaEmbodimentIntentParser.Parse(assistantMessage, userMessage);

            try
            {
                await _virtualEnvironment.CompanionExchangeAsync(userMessage, assistantMessage).ConfigureAwait(false);

                var talkSeconds = Math.Clamp(assistantMessage.Length / 14, 2, 45);
                await _virtualEnvironment.AnimateAvatarAsync(avatarId, "Talk").ConfigureAwait(false);
                await _virtualEnvironment.AnimateAvatarAsync(avatarId, "LipSync_Talking").ConfigureAwait(false);

                if (intents.WantsWalk)
                    await _virtualEnvironment.SendCommandAsync($"wander {avatarId} {talkSeconds}").ConfigureAwait(false);

                if (intents.WantsSee)
                    await RequestSceneCaptureAsync(cancellationToken).ConfigureAwait(false);

                if (intents.WantsTouch)
                {
                    var target = SanitizeTouchTarget(intents.TouchTarget);
                    await _virtualEnvironment.TouchInteractAsync(avatarId, target, "touch").ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VictoriaEmbodiment: OnChatExchange failed: {ex.Message}");
            }
        }

        public async Task RequestSceneCaptureAsync(CancellationToken cancellationToken = default)
        {
            if (!await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false))
                return;

            try
            {
                await _virtualEnvironment.SendCommandAsync("get_scene_info").ConfigureAwait(false);
                await _virtualEnvironment.SendCommandAsync("capture_scene").ConfigureAwait(false);
                await _virtualEnvironment.LookAtAsync(AvatarId, 0, 0, 1).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VictoriaEmbodiment: scene capture failed: {ex.Message}");
            }
        }

        private string AvatarId =>
            string.IsNullOrWhiteSpace(_appConfig.VictoriaUnrealAvatarId)
                ? "victoria"
                : _appConfig.VictoriaUnrealAvatarId.Trim();

        private async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var status = await _virtualEnvironment.GetStatusAsync().ConfigureAwait(false);
            if (status.IsConnected)
                return true;

            var endpoint = _appConfig.UnrealEngineEndpoint;
            if (string.IsNullOrWhiteSpace(endpoint))
                return false;

            return await _virtualEnvironment.ConnectAsync(endpoint).ConfigureAwait(false);
        }

        private async Task<bool> ShouldEmbodyContactAsync(string contactId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_persistence == null)
                return true;

            try
            {
                var contacts = await _persistence.GetAllAsync<AIContact>().ConfigureAwait(false);
                if (!contacts.TryGetValue(contactId, out var contact))
                    return false;

                return contact.IsPrimaryAI
                    || contact.Name.Contains("victoria", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VictoriaEmbodiment: contact lookup failed: {ex.Message}");
                return false;
            }
        }

        private static string SanitizeTouchTarget(string? target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return "nearby";

            var cleaned = target.Replace('\n', ' ').Replace('\r', ' ').Trim();
            foreach (var ch in new[] { '"', '\'', ';', '|' })
                cleaned = cleaned.Replace(ch.ToString(), string.Empty, StringComparison.Ordinal);

            return cleaned.Length > 80 ? cleaned[..80] : cleaned;
        }
    }
}
