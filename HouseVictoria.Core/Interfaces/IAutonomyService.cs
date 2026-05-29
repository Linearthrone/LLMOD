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
        CognitionVitalsSnapshot GetVitals();
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync();
        /// <summary>Temporarily override vitals (e.g. active trading).</summary>
        void PushVitalOverride(CognitionVitalRhythm rhythm, string label, TimeSpan? duration = null);
        event EventHandler<AutonomyActivityEventArgs>? ActivityCompleted;
        event EventHandler<CognitionVitalsChangedEventArgs>? VitalsChanged;
    }
}
