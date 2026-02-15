using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HouseVictoria.App.HelperClasses
{
    /// <summary>
    /// Helper class for window minimize/restore functionality and auto-sizing
    /// </summary>
    public static class WindowHelper
    {
        public static void MinimizeToTray(Window window, ref bool isMinimized, ref double savedWidth, 
            ref double savedHeight, ref double savedLeft, ref double savedTop)
        {
            if (!isMinimized)
            {
                // Save current state
                savedWidth = window.Width;
                savedHeight = window.Height;
                savedLeft = window.Left;
                savedTop = window.Top;
                
                // Hide window completely
                window.Visibility = Visibility.Hidden;
                isMinimized = true;
            }
        }

        public static void RestoreFromTray(Window window, ref bool isMinimized, 
            double savedWidth, double savedHeight, double savedLeft, double savedTop)
        {
            if (isMinimized)
            {
                // Restore from minimized state
                window.Width = savedWidth;
                window.Height = savedHeight;
                window.Left = savedLeft;
                window.Top = savedTop;
                window.Visibility = Visibility.Visible;
                isMinimized = false;
                window.Activate();
                window.Focus();
            }
        }

        /// <summary>
        /// Auto-sizes a window to fit its content without scrolling, except for text that can wrap.
        /// Measures the content area and adjusts window size accordingly.
        /// </summary>
        public static void AutoSizeWindowToContent(Window window, FrameworkElement contentElement)
        {
            if (window == null || contentElement == null)
                return;

            try
            {
                // Force layout update first
                window.UpdateLayout();
                contentElement.UpdateLayout();

                // Measure the desired size of the content
                contentElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var desiredSize = contentElement.DesiredSize;

                // Get the window's non-client area (border, title bar, etc.)
                var windowBorder = SystemParameters.WindowNonClientFrameThickness;
                var titleBarHeight = SystemParameters.WindowCaptionHeight;

                // Calculate the actual content area size needed
                var contentWidth = desiredSize.Width;
                var contentHeight = desiredSize.Height;

                // Account for window chrome
                var totalWidth = contentWidth + windowBorder.Left + windowBorder.Right;
                var totalHeight = contentHeight + titleBarHeight + windowBorder.Top + windowBorder.Bottom;

                // Get screen dimensions
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;

                // Ensure window doesn't exceed screen bounds
                totalWidth = Math.Min(totalWidth, screenWidth * 0.95);
                totalHeight = Math.Min(totalHeight, screenHeight * 0.95);

                // Set minimum sizes
                totalWidth = Math.Max(totalWidth, window.MinWidth > 0 ? window.MinWidth : 300);
                totalHeight = Math.Max(totalHeight, window.MinHeight > 0 ? window.MinHeight : 200);

                // Update window size
                window.Width = totalWidth;
                window.Height = totalHeight;

                // Center window if it's not already positioned
                if (double.IsNaN(window.Left) || double.IsNaN(window.Top))
                {
                    window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error auto-sizing window: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensures the window fits within the screen's working area (excludes taskbar).
        /// Call from Loaded event to prevent windows from being cut off or positioned off-screen.
        /// </summary>
        public static void EnsureWindowFitsOnScreen(Window window)
        {
            if (window == null) return;

            var workArea = SystemParameters.WorkArea;

            // Constrain size to fit (leave 5% margin)
            var maxWidth = workArea.Width * 0.95;
            var maxHeight = workArea.Height * 0.95;
            if (window.Width > maxWidth) window.Width = maxWidth;
            if (window.Height > maxHeight) window.Height = maxHeight;

            // Ensure position keeps window fully visible
            if (window.Left < workArea.Left) window.Left = workArea.Left;
            if (window.Top < workArea.Top) window.Top = workArea.Top;
            if (window.Left + window.Width > workArea.Right) window.Left = workArea.Right - window.Width;
            if (window.Top + window.Height > workArea.Bottom) window.Top = workArea.Bottom - window.Height;
        }

        /// <summary>
        /// Configures a ScrollViewer to fit its content instead of scrolling.
        /// Text elements within will still wrap normally.
        /// </summary>
        public static void ConfigureScrollViewerToFitContent(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null)
                return;

            // Hide scrollbars
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

            // Set content to stretch to fit
            if (scrollViewer.Content is FrameworkElement content)
            {
                content.HorizontalAlignment = HorizontalAlignment.Stretch;
                content.VerticalAlignment = VerticalAlignment.Top;
            }
        }
    }
}
