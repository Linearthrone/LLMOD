using System;
using System.Windows;
using System.Windows.Threading;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace HouseVictoria.App.Services
{
    /// <summary>
    /// Local webcam preview using OpenCv (no WebRTC peer connection).
    /// </summary>
    public sealed class LocalCameraPreviewService : IDisposable
    {
        private VideoCapture? _capture;
        private DispatcherTimer? _timer;
        private bool _disposed;

        public bool IsRunning => _timer != null && _timer.IsEnabled;

        public void Start(System.Windows.Controls.Image target, Dispatcher dispatcher, int cameraIndex = 0)
        {
            if (_disposed || target == null)
                return;

            Stop();

            try
            {
                _capture = new VideoCapture(cameraIndex);
                if (!_capture.IsOpened())
                {
                    target.ToolTip = "No camera found or could not open device.";
                    return;
                }

                _timer = new DispatcherTimer(DispatcherPriority.Render, dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(33)
                };
                _timer.Tick += (_, _) =>
                {
                    if (_capture == null || !_capture.IsOpened())
                        return;
                    using var mat = new Mat();
                    if (!_capture.Read(mat) || mat.Empty())
                        return;
                    try
                    {
                        var bmp = BitmapSourceConverter.ToBitmapSource(mat);
                        bmp.Freeze();
                        target.Source = bmp;
                    }
                    catch
                    {
                        // ignore frame conversion errors
                    }
                };
                _timer.Start();
            }
            catch (Exception ex)
            {
                target.ToolTip = $"Camera error: {ex.Message}";
            }
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer = null;
            _capture?.Dispose();
            _capture = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Stop();
        }
    }
}
