using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// High-level cognitive agent service that coordinates
    /// AI planning and virtual environment actions.
    /// </summary>
    public interface IAgentService
    {
        /// <summary>
        /// Friendly name of the agent (e.g., "Ava").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Initialize any required resources and connect to the
        /// underlying virtual environment if needed.
        /// </summary>
        Task InitializeAsync(string? virtualEnvironmentEndpoint = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Run a single agent step: perceive, decide, and act.
        /// </summary>
        Task<AgentStepResult> StepAsync(string? userInput = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the current high-level state snapshot for UI/debugging.
        /// </summary>
        AgentState GetCurrentState();
    }

    /// <summary>
    /// Simple snapshot of the agent's current state.
    /// </summary>
    public class AgentState
    {
        public string Name { get; set; } = "Ava";
        public string? CurrentGoal { get; set; }
        public string? LastAction { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public Dictionary<string, double> Drives { get; set; } = new();
        public string? EnvironmentScene { get; set; }
        public bool IsEnvironmentConnected { get; set; }
    }

    /// <summary>
    /// Result from a single agent step.
    /// </summary>
    public class AgentStepResult
    {
        public string? Goal { get; set; }
        public string? PlanDescription { get; set; }
        public string? ActionDescription { get; set; }
        public AgentState StateSnapshot { get; set; } = new();
    }
}

