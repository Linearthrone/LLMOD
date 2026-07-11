using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Trays
{
    public class InstrumentStackDrawerViewModel : ObservableObject, IDisposable
    {
        public const int VitalsTabIndex = 0;
        public const int ControlTabIndex = 1;
        public const int HealthTabIndex = 2;
        public const int ComponentsTabIndex = 3;
        public const int DesktopTabIndex = 4;

        public SystemMonitorDrawerViewModel System { get; }
        public CognitionVitalsDrawerViewModel Vitals { get; }
        public AgentDesktopDrawerViewModel Desktop { get; }

        private readonly IAgentDesktopMonitorService? _agentDesktopMonitor;

        public ICommand OpenVitalsTabCommand { get; }
        public ICommand OpenDesktopTabCommand { get; }
        public ICommand ExpandFromHandleCommand { get; }

        public InstrumentStackDrawerViewModel(
            ISystemMonitorService systemMonitorService,
            Border drawerPanel,
            Border vitalsDrawerStub,
            IAgentDesktopMonitorService? agentDesktopMonitor = null)
        {
            System = new SystemMonitorDrawerViewModel(
                systemMonitorService,
                drawerPanel,
                HealthTabIndex,
                ComponentsTabIndex);

            Vitals = new CognitionVitalsDrawerViewModel(vitalsDrawerStub, managesDrawerPanel: false)
            {
                CollapseState = VitalsDrawerCollapseState.Pulse
            };

            Desktop = new AgentDesktopDrawerViewModel(agentDesktopMonitor);
            _agentDesktopMonitor = agentDesktopMonitor;

            OpenVitalsTabCommand = new RelayCommand(OpenVitalsTab);
            OpenDesktopTabCommand = new RelayCommand(OpenDesktopTab);
            ExpandFromHandleCommand = new RelayCommand(() =>
            {
                Vitals.CollapseState = VitalsDrawerCollapseState.Pulse;
            });

            if (agentDesktopMonitor != null)
                agentDesktopMonitor.SessionChanged += AgentDesktopMonitor_SessionChanged;
        }

        private void AgentDesktopMonitor_SessionChanged(object? sender, AgentDesktopSessionChangedEventArgs e)
        {
            if (e.IsActive)
                OpenDesktopTab();
        }

        public void OpenVitalsTab()
        {
            System.SelectedTabIndex = VitalsTabIndex;
            System.IsDrawerOpen = true;
        }

        public void OpenControlTab()
        {
            System.SelectedTabIndex = ControlTabIndex;
            System.IsDrawerOpen = true;
        }

        public void OpenDesktopTab()
        {
            System.IsDrawerOpen = true;
            if (System.SelectedTabIndex != DesktopTabIndex)
                System.SelectedTabIndex = DesktopTabIndex;
            else
                OnDesktopTabSelected();
        }

        public void OnDesktopTabSelected()
        {
            _agentDesktopMonitor?.RequestPreview();
        }

        public void CollapseDrawerToPulse()
        {
            System.IsDrawerOpen = false;
            if (Vitals.CollapseState == VitalsDrawerCollapseState.Open)
                Vitals.CollapseState = VitalsDrawerCollapseState.Pulse;
        }

        public void CollapseToHandle()
        {
            System.IsDrawerOpen = false;
            Vitals.CollapseState = VitalsDrawerCollapseState.Handle;
        }

        public void Dispose()
        {
            if (_agentDesktopMonitor != null)
            {
                _agentDesktopMonitor.SessionChanged -= AgentDesktopMonitor_SessionChanged;
                _agentDesktopMonitor.ReleasePreview();
            }

            System.Dispose();
            Vitals.Dispose();
            Desktop.Dispose();
        }

        public void OnDesktopTabDeselected()
        {
            _agentDesktopMonitor?.ReleasePreview();
        }
    }
}
