using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HouseVictoria.App.HelperClasses
{
    /// <summary>
    /// Base class for overlay windows that are click-through
    /// </summary>
    public class OverlayWindow : Window
    {
        #region Native Methods

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const uint LWA_ALPHA = 0x00000002;

        #endregion

        protected bool IsClickThrough { get; set; } = false;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            if (IsClickThrough)
            {
                SetClickThrough(true);
            }
        }

        protected void SetClickThrough(bool isClickThrough)
        {
            var helper = new WindowInteropHelper(this);
            
            // Ensure window handle is valid
            if (helper.Handle == IntPtr.Zero)
            {
                // Handle not yet created, will be set in OnSourceInitialized
                return;
            }

            var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);

            if (isClickThrough)
            {
                // WS_EX_TRANSPARENT requires WS_EX_LAYERED to be set first
                exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
            }
            else
            {
                // Remove transparent flag but keep layered (needed for opacity)
                exStyle &= ~WS_EX_TRANSPARENT;
                // Only set WS_EX_LAYERED if we're using opacity
                // Otherwise, we can remove it: exStyle &= ~WS_EX_LAYERED;
            }

            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle);
        }

        protected void SetWindowOpacity(double opacity)
        {
            var helper = new WindowInteropHelper(this);
            
            // Ensure window handle is valid
            if (helper.Handle == IntPtr.Zero)
            {
                return;
            }

            // SetLayeredWindowAttributes requires WS_EX_LAYERED to be set first
            var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            if ((exStyle & WS_EX_LAYERED) == 0)
            {
                exStyle |= WS_EX_LAYERED;
                SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle);
            }

            SetLayeredWindowAttributes(helper.Handle, 0, (byte)(opacity * 255), LWA_ALPHA);
        }
    }
}
