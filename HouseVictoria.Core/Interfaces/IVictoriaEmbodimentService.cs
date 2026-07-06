namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Routes Victoria's cognition (chat, autonomy) to the embodied MetaHuman in Unreal.
    /// </summary>
    public interface IVictoriaEmbodimentService
    {
        /// <summary>Connect to Unreal, focus the configured avatar, and push locomotion defaults.</summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>After a chat turn, drive talk/walk/see/touch on the avatar when connected.</summary>
        Task OnChatExchangeAsync(
            string contactId,
            string userMessage,
            string assistantMessage,
            CancellationToken cancellationToken = default);

        /// <summary>Request a scene snapshot from Unreal (see).</summary>
        Task RequestSceneCaptureAsync(CancellationToken cancellationToken = default);
    }
}
