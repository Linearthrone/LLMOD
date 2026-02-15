using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using System.Text;

namespace HouseVictoria.Services.FileGeneration
{
    /// <summary>
    /// Service for creating and managing AI-generated files
    /// </summary>
    public class FileGenerationService : IFileGenerationService
    {
        private readonly string _basePath;
        private readonly string _generatedFilesPath;

        public FileGenerationService(string basePath = "Media")
        {
            _basePath = basePath;
            _generatedFilesPath = System.IO.Path.Combine(basePath, "GeneratedFiles");
            
            // Ensure the directory exists
            if (!System.IO.Directory.Exists(_generatedFilesPath))
            {
                System.IO.Directory.CreateDirectory(_generatedFilesPath);
            }
        }

        public async Task<string> CreateTextFileAsync(string fileName, string content, string? subdirectory = null)
        {
            try
            {
                // Ensure .txt extension if not present
                if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".txt";
                }

                var filePath = await PrepareFilePathAsync(fileName, subdirectory);
                await System.IO.File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
                
                System.Diagnostics.Debug.WriteLine($"Created file: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating text file '{fileName}': {ex.Message}");
                throw;
            }
        }

        public async Task<string> CreateFileAsync(string fileName, byte[] content, string? subdirectory = null)
        {
            try
            {
                var filePath = await PrepareFilePathAsync(fileName, subdirectory);
                await System.IO.File.WriteAllBytesAsync(filePath, content);
                
                System.Diagnostics.Debug.WriteLine($"Created file: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating file '{fileName}': {ex.Message}");
                throw;
            }
        }

        private Task<string> PrepareFilePathAsync(string fileName, string? subdirectory)
        {
            // Sanitize filename
            fileName = SanitizeFileName(fileName);

            var targetDir = string.IsNullOrWhiteSpace(subdirectory)
                ? _generatedFilesPath
                : System.IO.Path.Combine(_generatedFilesPath, subdirectory);

            // Ensure directory exists
            if (!System.IO.Directory.Exists(targetDir))
            {
                System.IO.Directory.CreateDirectory(targetDir);
            }

            var filePath = System.IO.Path.Combine(targetDir, fileName);
            
            // If file exists, append timestamp to make it unique
            if (System.IO.File.Exists(filePath))
            {
                var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);
                var ext = System.IO.Path.GetExtension(fileName);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                fileName = $"{nameWithoutExt}_{timestamp}{ext}";
                filePath = System.IO.Path.Combine(targetDir, fileName);
            }

            return Task.FromResult(filePath);
        }

        public async Task<List<GeneratedFile>> GetGeneratedFilesAsync()
        {
            var files = new List<GeneratedFile>();
            
            try
            {
                if (!System.IO.Directory.Exists(_generatedFilesPath))
                {
                    return files;
                }

                var filePaths = System.IO.Directory.GetFiles(_generatedFilesPath, "*", System.IO.SearchOption.AllDirectories);
                
                foreach (var filePath in filePaths)
                {
                    var fileInfo = new System.IO.FileInfo(filePath);
                    files.Add(new GeneratedFile
                    {
                        FileName = System.IO.Path.GetFileName(filePath),
                        FilePath = filePath,
                        FileSize = fileInfo.Length,
                        CreatedAt = fileInfo.CreationTime
                    });
                }

                // Sort by creation time, newest first
                files = files.OrderByDescending(f => f.CreatedAt).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting generated files: {ex.Message}");
            }

            return await Task.FromResult(files);
        }

        public async Task<string?> GetFilePathAsync(string fileName)
        {
            try
            {
                if (!System.IO.Directory.Exists(_generatedFilesPath))
                {
                    return null;
                }

                var filePath = System.IO.Path.Combine(_generatedFilesPath, fileName);
                if (System.IO.File.Exists(filePath))
                {
                    return await Task.FromResult<string?>(filePath);
                }

                // Search in subdirectories
                var files = System.IO.Directory.GetFiles(_generatedFilesPath, fileName, System.IO.SearchOption.AllDirectories);
                return files.Length > 0 ? await Task.FromResult<string?>(files[0]) : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting file path for '{fileName}': {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DeleteFileAsync(string fileName)
        {
            try
            {
                var filePath = await GetFilePathAsync(fileName);
                if (filePath != null && System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    System.Diagnostics.Debug.WriteLine($"Deleted file: {filePath}");
                    return await Task.FromResult(true);
                }
                return await Task.FromResult(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting file '{fileName}': {ex.Message}");
                return await Task.FromResult(false);
            }
        }

        private string SanitizeFileName(string fileName)
        {
            // Remove invalid characters
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }
            
            // Remove leading/trailing spaces and dots
            fileName = fileName.Trim().Trim('.');
            
            // Ensure it's not empty
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"generated_file_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            }
            
            return fileName;
        }
    }
}
