using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.Agent
{
    /// <summary>
    /// Default implementation of <see cref="IAgentService"/> that composes
    /// the AI service (LLM) and the Unreal virtual environment service.
    /// 
    /// This is intentionally lightweight and host-facing: it does not try
    /// to implement a full cognitive architecture, but provides a clean
    /// seam where such logic can live while still integrating with
    /// House Victoria services.
    /// </summary>
    public class AgentService : IAgentService
    {
        private readonly IAIService _aiService;
        private readonly IVirtualEnvironmentService _virtualEnvironmentService;

        private readonly AgentState _state = new();
        private readonly Dictionary<string, double> _drives = new()
        {
            ["social"] = 0.5,
            ["curiosity"] = 0.5,
            ["boredom"] = 0.2
        };

        public string Name => _state.Name;

        public AgentService(IAIService aiService, IVirtualEnvironmentService virtualEnvironmentService)
        {
            _aiService = aiService;
            _virtualEnvironmentService = virtualEnvironmentService;
        }

        public async Task InitializeAsync(string? virtualEnvironmentEndpoint = null, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(virtualEnvironmentEndpoint))
            {
                await _virtualEnvironmentService.ConnectAsync(virtualEnvironmentEndpoint);
            }

            var status = await _virtualEnvironmentService.GetStatusAsync();
            _state.IsEnvironmentConnected = status.IsConnected;
            _state.EnvironmentScene = status.CurrentScene;
            _state.LastUpdated = DateTime.UtcNow;
        }

        public async Task<AgentStepResult> StepAsync(string? userInput = null, CancellationToken cancellationToken = default)
        {
            // 1) Perceive environment
            var status = await _virtualEnvironmentService.GetStatusAsync();
            _state.IsEnvironmentConnected = status.IsConnected;
            _state.EnvironmentScene = status.CurrentScene;

            // 2) Simple drive update
            if (status.AvatarCount > 0)
            {
                _drives["social"] = Math.Min(1.0, _drives["social"] + 0.05);
                _drives["boredom"] = Math.Max(0.0, _drives["boredom"] - 0.02);
            }
            else
            {
                _drives["curiosity"] = Math.Min(1.0, _drives["curiosity"] + 0.03);
                _drives["boredom"] = Math.Min(1.0, _drives["boredom"] + 0.02);
            }

            _state.Drives = new Dictionary<string, double>(_drives);

            // 3) Pick a very simple goal
            string goal;
            if (_drives["social"] > 0.6 && status.AvatarCount == 0)
            {
                goal = "spawn_avatar";
            }
            else if (_drives["curiosity"] > 0.6)
            {
                goal = "inspect_scene";
            }
            else if (_drives["boredom"] > 0.5)
            {
                goal = "wander";
            }
            else
            {
                goal = "idle";
            }

            _state.CurrentGoal = goal;

            // 4) Very small "plan" and action
            string planDescription;
            string actionDescription;

            if (goal == "spawn_avatar")
            {
                var avatar = new AvatarDefinition
                {
                    Name = "Ava",
                    ModelPath = "DefaultAvatar",
                    Position = new Vector3(0, 0, 0)
                };

                await _virtualEnvironmentService.SpawnAvatarAsync(avatar);
                planDescription = "Spawn a default avatar in the current scene.";
                actionDescription = "Spawned avatar 'Ava'.";
            }
            else if (goal == "inspect_scene")
            {
                await _virtualEnvironmentService.GetSceneInformationAsync();
                planDescription = "Request scene information from the virtual environment.";
                actionDescription = "Requested scene info.";
            }
            else if (goal == "wander")
            {
                await _virtualEnvironmentService.SendCommandAsync("wander");
                planDescription = "Issue a simple wander command to the environment.";
                actionDescription = "Sent wander command.";
            }
            else
            {
                planDescription = "Remain idle this step.";
                actionDescription = "No action taken.";
            }

            _state.LastAction = actionDescription;
            _state.LastUpdated = DateTime.UtcNow;

            return new AgentStepResult
            {
                Goal = goal,
                PlanDescription = planDescription,
                ActionDescription = actionDescription,
                StateSnapshot = GetCurrentState()
            };
        }

        public AgentState GetCurrentState()
        {
            return new AgentState
            {
                Name = _state.Name,
                CurrentGoal = _state.CurrentGoal,
                LastAction = _state.LastAction,
                LastUpdated = _state.LastUpdated,
                Drives = new Dictionary<string, double>(_state.Drives),
                EnvironmentScene = _state.EnvironmentScene,
                IsEnvironmentConnected = _state.IsEnvironmentConnected
            };
        }
    }
}

