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

        public static readonly DependencyProperty SubjectsProperty =
            DependencyProperty.Register(nameof(Subjects), typeof(IReadOnlyList<CognitionThoughtSubject>), typeof(CognitionVitalsControl),
                new PropertyMetadata(null, OnSubjectsChanged));

        private readonly DispatcherTimer _animTimer;
        private readonly List<WaveLane> _lanes = new();
        private int _sampleCapacity = 120;

        private sealed class WaveLane
        {
            public required Polyline Polyline { get; init; }
            public required List<double> Samples { get; init; }
            public double Phase { get; set; }
            public CognitionThoughtSubject Subject { get; set; } = null!;
        }

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

        public IReadOnlyList<CognitionThoughtSubject>? Subjects
        {
            get => (IReadOnlyList<CognitionThoughtSubject>?)GetValue(SubjectsProperty);
            set => SetValue(SubjectsProperty, value);
        }

        public CognitionVitalsControl()
        {
            InitializeComponent();

            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _animTimer.Tick += (_, _) => AdvanceWaveform();
            Loaded += (_, _) =>
            {
                _sampleCapacity = CompactMode ? 80 : 140;
                EnsureLanes();
                _animTimer.Start();
                SizeChanged += (_, _) => RedrawAll();
            };
            Unloaded += (_, _) => _animTimer.Stop();
        }

        private static void OnVitalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CognitionVitalsControl c)
            {
                c.EnsureLanes();
                c.UpdateBpmLabel();
            }
        }

        private static void OnSubjectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CognitionVitalsControl c)
            {
                c.EnsureLanes();
                c.UpdateBpmLabel();
            }
        }

        private void EnsureLanes()
        {
            var subjectList = Subjects?.Where(s => s.AttentionWeight > 0.12).Take(4).ToList();
            var laneCount = subjectList is { Count: > 0 } ? subjectList.Count : 1;

            while (_lanes.Count < laneCount)
            {
                var polyline = new Polyline
                {
                    StrokeThickness = CompactMode ? 2.5 : 2.0,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                WaveCanvas.Children.Add(polyline);
                var samples = new List<double>();
                while (samples.Count < _sampleCapacity)
                    samples.Add(0.5);
                _lanes.Add(new WaveLane { Polyline = polyline, Samples = samples, Phase = 0 });
            }

            while (_lanes.Count > laneCount)
            {
                var last = _lanes[^1];
                WaveCanvas.Children.Remove(last.Polyline);
                _lanes.RemoveAt(_lanes.Count - 1);
            }

            if (subjectList is { Count: > 0 })
            {
                for (var i = 0; i < subjectList.Count; i++)
                {
                    var lane = _lanes[i];
                    var subject = subjectList[i];
                    lane.Subject = subject;
                    lane.Phase = PhaseSeed(subject.Id);
                    ApplyStroke(lane.Polyline, subject.WaveColorHex, subject.AttentionWeight);
                }

                for (var i = subjectList.Count; i < _lanes.Count; i++)
                    _lanes[i].Polyline.Visibility = Visibility.Collapsed;
            }
            else
            {
                var lane = _lanes[0];
                lane.Subject = new CognitionThoughtSubject
                {
                    Id = "fallback",
                    Rhythm = Rhythm,
                    BeatsPerMinute = BeatsPerMinute,
                    Intensity = Intensity,
                    WaveColorHex = WaveColorHex,
                    AttentionWeight = 1.0
                };
                ApplyStroke(lane.Polyline, WaveColorHex, 1.0);
            }

            foreach (var lane in _lanes)
                lane.Polyline.Visibility = Visibility.Visible;

            RedrawAll();
            UpdateBpmLabel();
        }

        private static double PhaseSeed(string id)
        {
            var hash = 0;
            foreach (var ch in id)
                hash = (hash * 31 + ch) % 628;
            return hash / 100.0;
        }

        private void ApplyStroke(Polyline polyline, string colorHex, double weight)
        {
            try
            {
                var converted = new BrushConverter().ConvertFromString(colorHex);
                if (converted is SolidColorBrush solid)
                {
                    var brush = new SolidColorBrush(solid.Color)
                    {
                        Opacity = Math.Clamp(0.35 + weight * 0.55, 0.35, 1.0)
                    };
                    if (brush.CanFreeze)
                        brush.Freeze();
                    polyline.Stroke = brush;
                }
                else
                    polyline.Stroke = Brushes.Cyan;
            }
            catch
            {
                polyline.Stroke = Brushes.Cyan;
            }

            polyline.StrokeThickness = CompactMode
                ? 2.0 + weight * 1.2
                : 1.4 + weight * 1.6;
        }

        private void UpdateBpmLabel()
        {
            if (Subjects is { Count: > 0 })
            {
                var top = Subjects.OrderByDescending(s => s.AttentionWeight).First();
                BpmLabel.Text = $"{top.BeatsPerMinute:F0} bpm · {Subjects.Count} thread{(Subjects.Count == 1 ? "" : "s")}";
            }
            else
            {
                BpmLabel.Text = $"{BeatsPerMinute:F0} bpm";
            }

            BpmLabel.Visibility = CompactMode ? Visibility.Collapsed : Visibility.Visible;
        }

        private void AdvanceWaveform()
        {
            if (_lanes.Count == 0)
                return;

            foreach (var lane in _lanes)
            {
                var subject = lane.Subject;
                var bpm = Math.Clamp(subject.BeatsPerMinute, 35, 130);
                var beatHz = bpm / 60.0;
                lane.Phase += beatHz * 0.04 * 2 * Math.PI;

                var mid = 0.5;
                var amp = Math.Clamp(subject.Intensity, 0.08, 1.0) * (CompactMode ? 0.32 : 0.38);
                amp *= Math.Clamp(subject.AttentionWeight, 0.2, 1.0);
                var y = mid + amp * SampleWave(subject.Rhythm, lane.Phase);
                y = Math.Clamp(y, 0.05, 0.95);

                lane.Samples.RemoveAt(0);
                lane.Samples.Add(y);
            }

            RedrawAll();
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

        private void RedrawAll()
        {
            var w = ActualWidth > 4 ? ActualWidth : (CompactMode ? 110 : 280);
            var h = WaveHeight;
            if (w <= 0 || h <= 0)
                return;

            foreach (var lane in _lanes)
            {
                var points = new PointCollection();
                var count = lane.Samples.Count;
                if (count < 2)
                    continue;

                for (var i = 0; i < count; i++)
                {
                    var x = i / (double)(count - 1) * w;
                    var yNorm = lane.Samples[i];
                    var y = (1.0 - yNorm) * (h - 4) + 2;
                    points.Add(new Point(x, y));
                }

                lane.Polyline.Points = points;
            }
        }
    }
}
