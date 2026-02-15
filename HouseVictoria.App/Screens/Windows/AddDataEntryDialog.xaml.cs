using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using HouseVictoria.Core.Models;
using Microsoft.Win32;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class AddDataEntryDialog : Window
    {
        private readonly DataBankEntry? _existingEntry;
        private string? _selectedAttachmentPath;
        private string? _existingAttachmentPath;
        private long? _existingAttachmentSize;
        private string? _existingAttachmentType;
        private bool _removeExistingAttachment;
        public DataBankEntry? ResultEntry { get; private set; }

        public AddDataEntryDialog(DataBankEntry? existingEntry = null)
        {
            _existingEntry = existingEntry;
            InitializeComponent();
            if (existingEntry != null)
            {
                Title = "Edit Data Entry";
                TitleTextBox.Text = existingEntry.Title;
                CategoryTextBox.Text = existingEntry.Category;
                ImportanceSlider.Value = existingEntry.Importance;
                TagsTextBox.Text = string.Join(", ", existingEntry.Tags ?? new List<string>());
                EntryTextBox.Text = existingEntry.Content;
                _existingAttachmentPath = existingEntry.AttachmentPath ?? existingEntry.AttachmentFileName;
                _existingAttachmentSize = existingEntry.AttachmentSizeBytes;
                _existingAttachmentType = existingEntry.AttachmentContentType;
                UpdateAttachmentInfo(_existingAttachmentPath, _existingAttachmentSize, _existingAttachmentType);
            }
            else
            {
                ImportanceSlider.Value = 0.5;
                UpdateAttachmentInfo(null, null, null);
            }
            EntryTextBox.Focus();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var content = EntryTextBox.Text?.Trim();
            var hasAttachment = (!string.IsNullOrWhiteSpace(_selectedAttachmentPath) && File.Exists(_selectedAttachmentPath)) ||
                                (!string.IsNullOrWhiteSpace(_existingAttachmentPath) && !_removeExistingAttachment);

            if (string.IsNullOrWhiteSpace(content) && !hasAttachment)
            {
                MessageBox.Show("Please enter content or attach a file for the data entry.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(content) && hasAttachment && !string.IsNullOrWhiteSpace(_selectedAttachmentPath))
            {
                content = $"[File] {System.IO.Path.GetFileName(_selectedAttachmentPath)}";
            }

            content ??= string.Empty;

            var tags = TagsTextBox.Text?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList() ?? new List<string>();

            string? attachmentPath = _existingAttachmentPath;
            string? attachmentFileName = _existingEntry?.AttachmentFileName;
            string? attachmentType = _existingEntry?.AttachmentContentType;
            long? attachmentSize = _existingEntry?.AttachmentSizeBytes;

            if (!string.IsNullOrWhiteSpace(_selectedAttachmentPath) && File.Exists(_selectedAttachmentPath))
            {
                attachmentPath = null; // will be filled after copy in service
                attachmentFileName = System.IO.Path.GetFileName(_selectedAttachmentPath);
                attachmentType = System.IO.Path.GetExtension(_selectedAttachmentPath)?.Trim('.').ToLowerInvariant();
                attachmentSize = new FileInfo(_selectedAttachmentPath).Length;
            }
            else if (_removeExistingAttachment)
            {
                attachmentPath = null;
                attachmentFileName = null;
                attachmentType = null;
                attachmentSize = null;
            }

            ResultEntry = new DataBankEntry
            {
                Id = _existingEntry?.Id ?? Guid.NewGuid().ToString(),
                Title = TitleTextBox.Text?.Trim() ?? string.Empty,
                Content = content,
                Category = string.IsNullOrWhiteSpace(CategoryTextBox.Text) ? null : CategoryTextBox.Text?.Trim(),
                Tags = tags,
                Importance = ImportanceSlider.Value,
                CreatedAt = _existingEntry?.CreatedAt ?? DateTime.Now,
                LastModified = DateTime.Now,
                AttachmentTempPath = File.Exists(_selectedAttachmentPath ?? string.Empty) ? _selectedAttachmentPath : null,
                AttachmentPath = attachmentPath,
                AttachmentFileName = attachmentFileName,
                AttachmentContentType = attachmentType,
                AttachmentSizeBytes = attachmentSize,
                AttachmentMarkedForRemoval = _removeExistingAttachment
            };

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BrowseAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                Title = "Select file to attach",
                Filter = "All files|*.*|Documents|*.pdf;*.txt;*.md;*.doc;*.docx;*.rtf;*.csv;*.json;*.xml|Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp"
            };

            var result = dialog.ShowDialog();
            if (result == true && File.Exists(dialog.FileName))
            {
                _selectedAttachmentPath = dialog.FileName;
                _removeExistingAttachment = false;
                var info = new FileInfo(dialog.FileName);
                UpdateAttachmentInfo(dialog.FileName, info.Length, info.Extension.Trim('.'));
            }
        }

        private void ClearAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedAttachmentPath = null;
            _removeExistingAttachment = !string.IsNullOrWhiteSpace(_existingAttachmentPath);
            UpdateAttachmentInfo(null, null, null);
        }

        private void UpdateAttachmentInfo(string? path, long? size, string? type)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                AttachmentInfoText.Text = "No file selected";
                return;
            }

            var name = System.IO.Path.GetFileName(path);
            var sizeText = size.HasValue ? $" - {FormatSize(size.Value)}" : string.Empty;
            var typeText = string.IsNullOrWhiteSpace(type) ? string.Empty : $" ({type})";
            AttachmentInfoText.Text = $"{name}{typeText}{sizeText}";
        }

        private static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.#} {sizes[order]}";
        }
    }
}
