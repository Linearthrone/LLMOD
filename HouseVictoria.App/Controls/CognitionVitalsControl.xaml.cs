using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Controls
{
    public partial class CognitionVitalsControl : UserControl
    {
        public static readonly DependencyProperty RhythmProperty =
            DependencyProperty.Register(nameof(Rhythm), typeof(CognitionVitalRhythm), typeof(CognitionVitalsControl),
                new PropertyMetadata(CognitionVitalRhythm.Resting, OnVitalChanged));

        public static readonly DependencyProperty BeatsPerMinuteProperty =
            DependencyProperty.Register(nameof(BeatsPerMinute), typeof(double), typeof(CognitionVitalsControl),
                new PropertyMetadata(52.0, OnVitalChanged));

        public static readonly DependencyProperty IntensityProperty =
            DependencyProperty.Register(nameof(Intensity), typeof(double), typeof(CognitionVitalsControl),
                new PropertyMetadata(0.3, OnVitalChanged));

        public static readonly DependencyProperty WaveColorHexProperty =
            DependencyProperty.Register(nameof(WaveColorHex), typeof(string), typeof(CognitionVitalsControl),
                new PropertyMetadata("#4FC3F7", OnVitalChanged));

        public static readonly DependencyProperty WaveHeightProperty =
            DependencyProperty.Register(nameof(WaveHeight), typeof(double), typeof(CognitionVitalsControl),
                new PropertyMetadata(56.0));

        public static readonly DependencyProperty CompactModeProperty =
            DependencyProperty.Register(nameof(CompactMode), typeof(bool), typeof(CognitionVitalsControl),
                new PropertyMetadata(false, OnVitalChanged));

        private readonly DispatcherTimer _animTimer;
        private readonly Polyline _polyline;
        private double _phase;
        private readonly List<double> _samples = new();
        private int _sampleCapacity = 120;

        public CognitionVitalRhythm Rhythm
        {
            get => (CognitionVitalRhythm)GetValue(RhythmProperty);
            set => SetValue(RhythmProperty, value);
        }

        public double BeatsPerMinute
        {
            get => (double)GetValue(BeatsPerMinuteProperty);
            set => SetValue(BeatsPerMinuteProperty, value);
        }

        public double Intensity
        {
            get => (double)GetValue(IntensityProperty);
            set => SetValue(IntensityProperty, value);
        }

        public string WaveColorHex
        {
            get => (string)GetValue(WaveColorHexProperty);
            set => SetValue(WaveColorHexProperty, value);
        }

        public double WaveHeight
        {
            get => (double)GetValue(WaveHeightProperty);
            set => SetValue(WaveHeightProperty, value);
        }

        public bool CompactMode
        {
            get => (bool)GetValue(CompactModeProperty);
            set => SetValue(CompactModeProperty, value);
        }

        public CognitionVitalsControl()
        {
            InitializeComponent();
            _polyline = new Polyline
            {
                StrokeThickness = CompactMode ? 3.0 : 2.0,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            WaveCanvas.Children.Add(_polyline);

            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _animTimer.Tick += (_, _) => AdvanceWaveform();
            Loaded += (_, _) =>
            {
                _sampleCapacity = CompactMode ? 80 : 140;
                while (_samples.Count < _sampleCapacity)
                    _samples.Add(0.5);
                ApplyStroke();
                _animTimer.Start();
                SizeChanged += (_, _) => Redraw();
            };
            Unloaded += (_, _) => _animTimer.Stop();
        }

        private static void OnVitalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CognitionVitalsControl c)
            {
                c.ApplyStroke();
                c.UpdateBpmLabel();
            }
        }

        private void ApplyStroke()
        {
            try
            {
                var converted = new BrushConverter().ConvertFromString(WaveColorHex);
                if (converted is Brush brush)
                {
                    if (brush.CanFreeze)
                        brush.Freeze();
                    _polyline.Stroke = brush;
                }
                else
                    _polyline.Stroke = Brushes.Cyan;
            }
            catch
            {
                _polyline.Stroke = Brushes.Cyan;
            }

            _polyline.StrokeThickness = CompactMode ? 3.0 : 2.0;
            BpmLabel.Visibility = CompactMode ? Visibility.Collapsed : Visibility.Visible;
            UpdateBpmLabel();
        }

        private void UpdateBpmLabel()
        {
            BpmLabel.Text = $"{BeatsPerMinute:F0} bpm";
        }

        private void AdvanceWaveform()
        {
            var bpm = Math.Clamp(BeatsPerMinute, 35, 130);
            var beatHz = bpm / 60.0;
            _phase += beatHz * 0.04 * 2 * Math.PI;

            var mid = 0.5;
            var amp = Math.Clamp(Intensity, 0.08, 1.0) * (CompactMode ? 0.35 : 0.42);
            var y = mid + amp * SampleWave(Rhythm, _phase);
            y = Math.Clamp(y, 0.05, 0.95);

            _samples.RemoveAt(0);
            _samples.Add(y);
            Redraw();
        }

        private static double SampleWave(CognitionVitalRhythm rhythm, double phase)
        {
            var t = phase % (2 * Math.PI);
            return rhythm switch
            {
                CognitionVitalRhythm.TradingActive => QrsComplex(t, sharpness: 14) + 0.15 * Math.Sin(t * 3),
                CognitionVitalRhythm.PriorityUrgent => QrsComplex(t, sharpness: 10) + 0.2 * Math.Sin(t * 2),
                CognitionVitalRhythm.ProjectWork => QrsComplex(t, sharpness: 8) + 0.12 * Math.Sin(t * 1.5),
                CognitionVitalRhythm.Environment => 0.7 * QrsComplex(t, sharpness: 6) + 0.25 * Math.Sin(t),
                CognitionVitalRhythm.Research => 0.55 * Math.Sin(t * 0.65) + 0.2 * Math.Sin(t * 1.3),
                CognitionVitalRhythm.CreativeCalm => 0.45 * Math.Sin(t * 0.5) + 0.1 * Math.Sin(t * 2.2),
                CognitionVitalRhythm.Reflecting => 0.35 * Math.Sin(t * 0.45),
                CognitionVitalRhythm.Waiting => 0.12 * Math.Sin(t * 0.35) + OccasionalBlip(t, 0.04),
                _ => 0.08 * Math.Sin(t * 0.3) + OccasionalBlip(t, 0.02)
            };
        }

        private static double QrsComplex(double t, double sharpness)
        {
            var x = (t / (2 * Math.PI)) % 1.0;
            if (x < 0.06)
                return -0.35 * Math.Sin(x / 0.06 * Math.PI);
            if (x < 0.09)
                return Math.Exp(-(x - 0.075) * sharpness) * 1.1;
            if (x < 0.18)
                return 0.15 * Math.Sin((x - 0.09) / 0.09 * Math.PI);
            return 0.05 * Math.Sin(t * 0.8);
        }

        private static double OccasionalBlip(double t, double rate)
        {
            var x = (t / (2 * Math.PI)) % 1.0;
            return x < rate ? 0.4 : 0;
        }

        private void Redraw()
        {
            var w = ActualWidth > 4 ? ActualWidth : (CompactMode ? 110 : 280);
            var h = WaveHeight;
            if (w <= 0 || h <= 0)
                return;

            var points = new PointCollection();
            var count = _samples.Count;
            if (count < 2)
                return;

            for (var i = 0; i < count; i++)
            {
                var x = i / (double)(count - 1) * w;
                var yNorm = _samples[i];
                var y = (1.0 - yNorm) * (h - 4) + 2;
                points.Add(new Point(x, y));
            }

            _polyline.Points = points;
        }
    }
}
