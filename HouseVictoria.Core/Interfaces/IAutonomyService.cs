using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Background autonomy loop: priority project work when assigned,
    /// idle-time creative and R&amp;D activities when the user is quiet.
    /// </summary>
    public interface IAutonomyService
    {
        AutonomyRuntimeState GetState();
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync();
        event EventHandler<AutonomyActivityEventArgs>? ActivityCompleted;
    }
}
