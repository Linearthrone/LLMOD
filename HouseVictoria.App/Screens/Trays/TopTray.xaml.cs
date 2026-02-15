using System.Windows;
using System.Windows.Controls;
using HouseVictoria.App.Screens.Windows;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Core.Utils;
using System.Linq;

namespace HouseVictoria.App.Screens.Trays
{
    public partial class TopTray : UserControl
    {
        private readonly IEventAggregator _eventAggregator;

        public TopTrayViewModel ViewModel { get; }

        public TopTray()
        {
            InitializeComponent();
            try
            {
                _eventAggregator = App.GetService<IEventAggregator>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get IEventAggregator: {ex.Message}");
                _eventAggregator = new EventAggregator();
            }

            ViewModel = new TopTrayViewModel(_eventAggregator);
            DataContext = ViewModel;
        }

        public void DataBankBox_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                e.Effects = System.Windows.DragDropEffects.Copy;
            else
                e.Effects = System.Windows.DragDropEffects.None;
        }
        
        public void DataBankBox_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
        }
        
        private async void DataBankBox_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                return;

            try
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files == null || files.Length == 0)
                    return;

                var memoryService = App.GetService<IMemoryService>();
                if (memoryService == null)
                {
                    System.Windows.MessageBox.Show("Memory service is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Get or create a "Dropped Files" data bank
                var allBanks = await memoryService.GetAllDataBanksAsync().ConfigureAwait(false);
                var droppedFilesBank = allBanks?.FirstOrDefault(b => b.Name == "Dropped Files");

                if (droppedFilesBank == null)
                {
                    droppedFilesBank = new DataBank
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = "Dropped Files",
                        Description = "Files dropped into the Top Tray",
                        CreatedAt = DateTime.Now,
                        LastModified = DateTime.Now,
                        DataEntries = new List<DataBankEntry>()
                    };
                    await memoryService.AddDataBankAsync(droppedFilesBank).ConfigureAwait(false);
                }

                int processedCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                foreach (var filePath in files)
                {
                    try
                    {
                        if (!System.IO.File.Exists(filePath))
                        {
                            errorCount++;
                            errors.Add($"File not found: {System.IO.Path.GetFileName(filePath)}");
                            continue;
                        }

                        // Read file content
                        string fileContent;
                        var fileInfo = new System.IO.FileInfo(filePath);
                        var fileExtension = fileInfo.Extension.ToLowerInvariant();

                        // Handle text files
                        if (fileExtension == ".txt" || fileExtension == ".md" || fileExtension == ".json" || 
                            fileExtension == ".xml" || fileExtension == ".csv" || fileExtension == ".log" ||
                            fileExtension == ".cs" || fileExtension == ".js" || fileExtension == ".py" ||
                            fileExtension == ".html" || fileExtension == ".css")
                        {
                            fileContent = await System.IO.File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                        }
                        else
                        {
                            // For binary files, store file path and metadata
                            fileContent = $"[FILE: {System.IO.Path.GetFileName(filePath)}]\n" +
                                         $"Path: {filePath}\n" +
                                         $"Size: {fileInfo.Length} bytes\n" +
                                         $"Type: {fileExtension}\n" +
                                         $"Modified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}";
                        }

                        // Add to data bank
                        var entryContent = $"File: {System.IO.Path.GetFileName(filePath)}\n" +
                                           $"Path: {filePath}\n" +
                                           $"Dropped: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                           $"---\n{fileContent}\n---\n";

                        var dataEntry = new DataBankEntry
                        {
                            Title = System.IO.Path.GetFileName(filePath),
                            Content = entryContent,
                            Category = fileExtension.Trim('.').ToUpperInvariant(),
                            Tags = new List<string> { "dropped-file", fileExtension.Trim('.').ToLowerInvariant() },
                            Importance = 0.5,
                            CreatedAt = DateTime.Now,
                            LastModified = DateTime.Now,
                            AttachmentTempPath = filePath,
                            AttachmentFileName = System.IO.Path.GetFileName(filePath),
                            AttachmentContentType = fileExtension.Trim('.').ToLowerInvariant(),
                            AttachmentSizeBytes = fileInfo.Length
                        };

                        await memoryService.AddDataToBankAsync(droppedFilesBank.Id, dataEntry).ConfigureAwait(false);
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        errors.Add($"Error processing {System.IO.Path.GetFileName(filePath)}: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Error processing file {filePath}: {ex.Message}");
                    }
                }

                // Show result message on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var message = $"Processed {processedCount} file(s) successfully.";
                    if (errorCount > 0)
                    {
                        message += $"\n\n{errorCount} error(s) occurred:\n" + string.Join("\n", errors.Take(5));
                        if (errors.Count > 5)
                        {
                            message += $"\n... and {errors.Count - 5} more.";
                        }
                    }

                    System.Windows.MessageBox.Show(
                        message,
                        "Files Processed",
                        MessageBoxButton.OK,
                        errorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error processing dropped files: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in DataBankBox_Drop: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async void FileRetrievalButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                var fileGenerationService = App.GetService<IFileGenerationService>();
                if (fileGenerationService == null)
                {
                    System.Windows.MessageBox.Show("File generation service is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var files = await fileGenerationService.GetGeneratedFilesAsync();
                
                if (files == null || files.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "No generated files available.\n\nFiles created by AI contacts will appear here.",
                        "File Retrieval",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Open the folder containing the generated files
                var filePath = files.FirstOrDefault()?.FilePath;
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    var folderPath = System.IO.Path.GetDirectoryName(filePath);
                    if (System.IO.Directory.Exists(folderPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = folderPath,
                            UseShellExecute = true,
                            Verb = "open"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error accessing generated files: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void GLDButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Open Global Log Directory window
            var gldWindow = new GlobalLogDirectoryWindow();
            gldWindow.Owner = System.Windows.Window.GetWindow(this);
            gldWindow.Show();
        }
    }
}
