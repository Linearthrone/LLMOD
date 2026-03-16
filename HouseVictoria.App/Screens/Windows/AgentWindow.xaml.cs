using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class AgentWindow : Window
    {
        public AgentViewModel ViewModel { get; }

        public AgentWindow()
        {
            InitializeComponent();

            var agentService = App.GetService<IAgentService>();
            var appConfig = App.GetService<AppConfig>();

            ViewModel = new AgentViewModel(agentService, appConfig);
            DataContext = ViewModel;
        }
    }

    public class AgentViewModel
    {
        private readonly IAgentService _agentService;
        private readonly AppConfig _config;

        public AgentState State { get; private set; } = new AgentState();

        public ICommand StepCommand { get; }

        public string DrivesSummary
        {
            get
            {
                if (State.Drives == null || State.Drives.Count == 0)
                    return "(no drives yet)";

                var parts = State.Drives
                    .Select(kv => $"{kv.Key}: {kv.Value:F2}");
                return string.Join(", ", parts);
            }
        }

        public AgentViewModel(IAgentService agentService, AppConfig config)
        {
            _agentService = agentService;
            _config = config;

            StepCommand = new RelayCommand(async _ => await StepAsync());

            // Initialize agent with Unreal endpoint from config
            _ = InitializeAsync();
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            try
            {
                await _agentService.InitializeAsync(_config.UnrealEngineEndpoint);
                State = _agentService.GetCurrentState();
                OnStateChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Agent initialization failed: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task StepAsync()
        {
            try
            {
                var result = await _agentService.StepAsync();
                State = result.StateSnapshot;
                OnStateChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Agent step failed: {ex.Message}");
            }
        }

        private void OnStateChanged()
        {
            // For this lightweight view model we simply raise a global refresh
            // by resetting the DataContext bindings via property notifications
            // if needed in the future. For now, bindings read from State/DrivesSummary.
        }
    }
}

