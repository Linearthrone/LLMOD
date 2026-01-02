using System;
using System.Windows;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware; // nuget install this

namespace GpuOverlay
{
    public partial class MainWindow : Window
    {
        private Computer _computer;
        private IHardware _gpu;

        public MainWindow()
        {
            InitializeComponent();
            InitHardware();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += UpdateGauge;
            timer.Start();
        }

        private void InitHardware()
        {
            _computer = new Computer { IsGpuEnabled = true };
            _computer.Open();
            // grab the first gpu found. if you have an igpu and a discrete, you might need logic to pick the discrete one.
            _gpu = _computer.Hardware[0];
        }

        private void UpdateGauge(object sender, EventArgs e)
        {
            _gpu.Update();
            // find the 'load' sensor
            var loadSensor = Array.Find(_gpu.Sensors, s => s.SensorType == SensorType.Load && s.Name == "GPU Core");

            float load = loadSensor?.Value ?? 0;
            UsageText.Text = $"{Math.Round(load)}%";
            UpdateArc(load);
        }

        private void UpdateArc(float percentage)
        {
            // map 0-100 to 180 degrees (PI radians)
            // start is at 180 deg (left), we sweep clockwise.
            // math: center is 100,100. radius is 80.

            double angle = (percentage / 100.0) * 180.0;
            double radians = (angle + 180) * (Math.PI / 180); // offset by 180 to start from left

            double x = 100 + 80 * Math.Cos(radians);
            double y = 100 + 80 * Math.Sin(radians);

            UsageSegment.Point = new Point(x, y);
            // isLargeArc is false because we only do half circle max
            UsageSegment.IsLargeArc = false;
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }
    }
}
