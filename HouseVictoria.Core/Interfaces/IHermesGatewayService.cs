namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Manages the Hermes Agent gateway process (OpenAI-compatible API on port 8642 by default).
    /// </summary>
    public interface IHermesGatewayService
    {
        /// <summary>GET /health — gateway reachable.</summary>
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

        /// <summary>Start <c>hermes gateway</c> when auto-start is enabled and health check fails.</summary>
        Task<bool> EnsureGatewayRunningAsync(CancellationToken cancellationToken = default);

        /// <summary>Health + authenticated /v1/models probe.</summary>
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    }
}
