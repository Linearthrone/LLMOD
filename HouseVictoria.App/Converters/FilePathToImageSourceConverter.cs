using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace HouseVictoria.App.Converters
{
    public class FilePathToImageSourceConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle file path string
            if (value is string filePath && !string.IsNullOrWhiteSpace(filePath))
            {
                // Check if it's an absolute path, if not try to make it absolute
                if (!Path.IsPathRooted(filePath))
                {
                    // Try to resolve relative path
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    filePath = Path.Combine(baseDir, filePath);
                }

                if (File.Exists(filePath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze(); // Freeze for thread safety
                        return bitmap;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading image from file path {filePath}: {ex.Message}");
                        return null;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Image file does not exist: {filePath}");
                    return null;
                }
            }

            // Handle byte array (MediaData)
            if (value is byte[] mediaData && mediaData.Length > 0)
            {
                try
                {
                    // Create a copy of the data for the bitmap
                    // BitmapImage requires the stream to remain open, so we create a persistent copy
                    var dataCopy = new byte[mediaData.Length];
                    Array.Copy(mediaData, dataCopy, mediaData.Length);
                    
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(dataCopy);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Freeze makes it thread-safe and allows stream disposal
                    return bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading image from byte array: {ex.Message}");
                    return null;
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
