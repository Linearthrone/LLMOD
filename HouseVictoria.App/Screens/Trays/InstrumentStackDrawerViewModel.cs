using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;

namespace HouseVictoria.App.Screens.Trays
{
    public class InstrumentStackDrawerViewModel : ObservableObject, IDisposable
    {
        public const int VitalsTabIndex = 0;
        public const int ControlTabIndex = 1;
        public const int HealthTabIndex = 2;
        public const int ComponentsTabIndex = 3;

        public SystemMonitorDrawerViewModel System { get; }
        public CognitionVitalsDrawerViewModel Vitals { get; }

        public ICommand OpenVitalsTabCommand { get; }
        public ICommand ExpandFromHandleCommand { get; }

        public InstrumentStackDrawerViewModel(
            ISystemMonitorService systemMonitorService,
            Border drawerPanel,
            Border vitalsDrawerStub)
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

            OpenVitalsTabCommand = new RelayCommand(OpenVitalsTab);
            ExpandFromHandleCommand = new RelayCommand(() =>
            {
                Vitals.CollapseState = VitalsDrawerCollapseState.Pulse;
            });
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
            System.Dispose();
            Vitals.Dispose();
        }
    }
}
