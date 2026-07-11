using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace HouseVictoria.Services.AgentDesktop
{
    internal static class WindowsScreenCapture
    {
        private const int SmXVirtualScreen = 76;
        private const int SmYVirtualScreen = 77;
        private const int SmCxVirtualScreen = 78;
        private const int SmCyVirtualScreen = 79;
        private const int SmCxScreen = 0;
        private const int SmCyScreen = 1;

        /// <summary>Captures the primary display and encodes it as PNG bytes.</summary>
        public static byte[]? CapturePrimaryScreenPng(int maxWidth = 1280)
        {
            var captured = CapturePrimaryScreen(maxWidth);
            if (captured == null)
                return null;

            var (width, height, pixels) = captured.Value;
            try
            {
                using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                var rect = new Rectangle(0, 0, width, height);
                var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var stride = data.Stride;
                    for (var row = 0; row < height; row++)
                        Marshal.Copy(pixels, row * width * 4, data.Scan0 + row * stride, width * 4);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public static (int Width, int Height, byte[] Bgra32)? CapturePrimaryScreen(int maxWidth = 960)
        {
            var width = GetSystemMetrics(SmCxScreen);
            var height = GetSystemMetrics(SmCyScreen);
            if (width <= 0 || height <= 0)
                return CaptureVirtualScreen(maxWidth);

            var captured = TryCopyFromScreen(0, 0, width, height)
                ?? TryBitBltCapture(0, 0, width, height);
            if (captured == null)
                return null;

            var (w, h, pixels) = captured.Value;
            if (w <= maxWidth)
                return (w, h, pixels);

            return Downscale(pixels, w, h, maxWidth);
        }

        /// <summary>Captures the whole virtual screen and encodes it as PNG bytes.</summary>
        public static byte[]? CaptureVirtualScreenPng(int maxWidth = 1280)
        {
            var captured = CaptureVirtualScreen(maxWidth);
            if (captured == null)
                return null;

            var (width, height, pixels) = captured.Value;
            try
            {
                using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                var rect = new Rectangle(0, 0, width, height);
                var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var stride = data.Stride;
                    for (var row = 0; row < height; row++)
                        Marshal.Copy(pixels, row * width * 4, data.Scan0 + row * stride, width * 4);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public static (int Width, int Height, byte[] Bgra32)? CaptureVirtualScreen(int maxWidth = 960)
        {
            var x = GetSystemMetrics(SmXVirtualScreen);
            var y = GetSystemMetrics(SmYVirtualScreen);
            var width = GetSystemMetrics(SmCxVirtualScreen);
            var height = GetSystemMetrics(SmCyVirtualScreen);
            if (width <= 0 || height <= 0)
                return null;

            var captured = TryCopyFromScreen(x, y, width, height)
                ?? TryBitBltCapture(x, y, width, height);
            if (captured == null)
                return null;

            var (w, h, pixels) = captured.Value;
            if (w <= maxWidth)
                return (w, h, pixels);

            return Downscale(pixels, w, h, maxWidth);
        }

        private static (int Width, int Height, byte[] Bgra32)? TryCopyFromScreen(int x, int y, int width, int height)
        {
            try
            {
                using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                }

                return ExtractBgra(bitmap);
            }
            catch
            {
                return null;
            }
        }

        private static (int Width, int Height, byte[] Bgra32)? TryBitBltCapture(int x, int y, int width, int height)
        {
            IntPtr desktopDc = IntPtr.Zero;
            IntPtr memDc = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;
            IntPtr old = IntPtr.Zero;

            try
            {
                var desktop = GetDesktopWindow();
                desktopDc = GetWindowDC(desktop);
                if (desktopDc == IntPtr.Zero)
                    return null;

                memDc = CreateCompatibleDC(desktopDc);
                if (memDc == IntPtr.Zero)
                    return null;

                bitmap = CreateCompatibleBitmap(desktopDc, width, height);
                if (bitmap == IntPtr.Zero)
                    return null;

                old = SelectObject(memDc, bitmap);
                if (!BitBlt(memDc, 0, 0, width, height, desktopDc, x, y, 0x00CC0020))
                    return null;

                var bmi = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = width,
                    Height = -height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0
                };

                var stride = width * 4;
                var pixels = new byte[stride * height];
                if (GetDIBits(memDc, bitmap, 0, (uint)height, pixels, ref bmi, 0) == 0)
                    return null;

                return (width, height, pixels);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (old != IntPtr.Zero)
                    SelectObject(memDc, old);
                if (bitmap != IntPtr.Zero)
                    DeleteObject(bitmap);
                if (memDc != IntPtr.Zero)
                    DeleteDC(memDc);
                if (desktopDc != IntPtr.Zero)
                    ReleaseDC(GetDesktopWindow(), desktopDc);
            }
        }

        public static (int Width, int Height, byte[] Bgra32)? DecodePngToBgra(byte[] png, int maxWidth = 960)
        {
            if (png == null || png.Length == 0)
                return null;

            try
            {
                using var ms = new MemoryStream(png);
                using var bitmap = new Bitmap(ms);
                var extracted = ExtractBgra(bitmap);
                if (extracted == null)
                    return null;

                var (w, h, pixels) = extracted.Value;
                if (w <= maxWidth)
                    return (w, h, pixels);

                return Downscale(pixels, w, h, maxWidth);
            }
            catch
            {
                return null;
            }
        }

        private static (int Width, int Height, byte[] Bgra32)? ExtractBgra(Bitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var rect = new Rectangle(0, 0, width, height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var stride = data.Stride;
                var pixels = new byte[width * height * 4];
                for (var row = 0; row < height; row++)
                {
                    Marshal.Copy(data.Scan0 + row * stride, pixels, row * width * 4, width * 4);
                }

                return (width, height, pixels);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static (int Width, int Height, byte[] Bgra32) Downscale(byte[] pixels, int width, int height, int maxWidth)
        {
            var scale = (double)maxWidth / width;
            var targetW = maxWidth;
            var targetH = Math.Max(1, (int)Math.Round(height * scale));
            var stride = width * 4;
            var scaled = new byte[targetW * targetH * 4];
            for (var row = 0; row < targetH; row++)
            {
                var srcRow = Math.Min(height - 1, (int)(row / scale));
                for (var col = 0; col < targetW; col++)
                {
                    var srcCol = Math.Min(width - 1, (int)(col / scale));
                    var srcIdx = srcRow * stride + srcCol * 4;
                    var dstIdx = row * targetW * 4 + col * 4;
                    scaled[dstIdx] = pixels[srcIdx];
                    scaled[dstIdx + 1] = pixels[srcIdx + 1];
                    scaled[dstIdx + 2] = pixels[srcIdx + 2];
                    scaled[dstIdx + 3] = 255;
                }
            }

            return (targetW, targetH, scaled);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint ImageSize;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr hdcDest, int xDest, int yDest, int width, int height,
            IntPtr hdcSrc, int xSrc, int ySrc, int rop);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(
            IntPtr hdc, IntPtr hbmp, uint startScan, uint scanLines,
            byte[] buffer, ref BitmapInfoHeader bmi, uint usage);
    }
}
