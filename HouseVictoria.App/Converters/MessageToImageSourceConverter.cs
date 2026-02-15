using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Converters
{
    /// <summary>
    /// Converter that extracts image source from a ConversationMessage object
    /// Checks both FilePath and MediaData properties
    /// </summary>
    public class MessageToImageSourceConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConversationMessage message)
            {
                // First try FilePath if it exists and is valid
                if (!string.IsNullOrWhiteSpace(message.FilePath))
                {
                    var filePath = message.FilePath;
                    
                    // Check if it's an absolute path, if not try to make it absolute
                    if (!Path.IsPathRooted(filePath))
                    {
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
                            bitmap.Freeze();
                            return bitmap;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error loading image from file path {filePath}: {ex.Message}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Image file does not exist: {filePath}");
                    }
                }

                // Fallback to MediaData if FilePath doesn't work and MediaData exists
                if (message.MediaData != null && message.MediaData.Length > 0 && 
                    (message.Type == MessageType.Image || message.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true))
                {
                    try
                    {
                        var dataCopy = new byte[message.MediaData.Length];
                        Array.Copy(message.MediaData, dataCopy, message.MediaData.Length);
                        
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = new MemoryStream(dataCopy);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading image from MediaData: {ex.Message}");
                    }
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