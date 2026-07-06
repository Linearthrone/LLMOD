using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Interface for Unreal Engine virtual environment interaction
    /// </summary>
    public interface IVirtualEnvironmentService
    {
        Task<bool> ConnectAsync(string endpoint);
        Task DisconnectAsync();
        Task<VirtualEnvironmentStatus> GetStatusAsync();
        Task<string> SendCommandAsync(string command);
        Task<string> GetSceneInformationAsync();
        Task<byte[]> CaptureSceneAsync();
        Task<string> SpawnAvatarAsync(AvatarDefinition avatar);
        Task<string> UpdateAvatarPoseAsync(string avatarId, Pose pose, string? facialExpression = null);
        Task<string> MoveAvatarAsync(string avatarId, float x, float y, float z, float rotationY);
        Task<string> AnimateAvatarAsync(string avatarId, string animationName);
        Task<Dictionary<string, object>> GetAvatarStateAsync(string avatarId);
        Task<string> SendJsonCommandAsync(object command);
        Task<string> FocusAvatarAsync(string avatarId);
        Task<string> SetLocomotionAsync(string avatarId, double walkSpeed, double runSpeed);
        Task<string> LookAtAsync(string avatarId, float x, float y, float z);
        Task<string> TouchInteractAsync(string avatarId, string target, string interaction = "touch");
        Task<string> CompanionExchangeAsync(string user, string assistant, string? correlationId = null);
        event EventHandler<VirtualEnvironmentEventArgs>? StatusChanged;
        event EventHandler<SceneUpdateEventArgs>? SceneUpdated;
    }

    public class VirtualEnvironmentEventArgs : EventArgs
    {
        public VirtualEnvironmentStatus Status { get; set; } = null!;
    }

    public class SceneUpdateEventArgs : EventArgs
    {
        public string SceneName { get; set; } = string.Empty;
        public string UpdateType { get; set; } = string.Empty;
        public object? Data { get; set; }
    }
}
