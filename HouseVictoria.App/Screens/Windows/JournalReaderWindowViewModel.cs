using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public class JournalReaderWindowViewModel : ObservableObject
    {
        private readonly IJournalService _journalService;
        private readonly IAIService? _aiService;
        private readonly IPersistenceService? _persistence;
        private readonly string _journalId;
        private ResearchJournal? _journal;
        private bool _isLoading;
        private bool _isConcluding;

        public ObservableCollection<JournalPageViewModel> Pages { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand ConcludeCommand { get; }
        public ICommand OpenLinkedFileCommand { get; }

        public string Title => _journal?.Title ?? "Journal";
        public string Preface => _journal?.Preface ?? string.Empty;
        public string StatusLabel => _journal?.Status == JournalStatus.Concluded ? "Concluded" : "In progress";
        public bool IsConcluded => _journal?.Status == JournalStatus.Concluded;
        public bool CanConclude => !IsConcluded && Pages.Count > 0;
        public string? ConclusionSummary => _journal?.ConclusionSummary;
        public string? ConclusionImplications => _journal?.ConclusionImplications;
        public bool HasConclusion => IsConcluded && !string.IsNullOrWhiteSpace(ConclusionSummary);

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsConcluding
        {
            get => _isConcluding;
            set => SetProperty(ref _isConcluding, value);
        }

        public JournalReaderWindowViewModel(string journalId, IJournalService journalService)
        {
            _journalId = journalId;
            _journalService = journalService ?? throw new ArgumentNullException(nameof(journalService));

            try { _aiService = App.GetService<IAIService>(); } catch { }
            try { _persistence = App.GetService<IPersistenceService>(); } catch { }

            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ConcludeCommand = new RelayCommand(async () => await ConcludeAsync(), () => CanConclude && !IsConcluding);
            OpenLinkedFileCommand = new RelayCommand<string?>(OpenLinkedFile);
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                _journal = await _journalService.GetJournalAsync(_journalId);
                Pages.Clear();

                if (_journal == null)
                    return;

                foreach (var entry in _journal.Entries
                             .Where(e => e.Kind != JournalEntryKind.Conclusion)
                             .OrderBy(e => e.Timestamp))
                {
                    Pages.Add(new JournalPageViewModel(entry));
                }

                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Preface));
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(IsConcluded));
                OnPropertyChanged(nameof(CanConclude));
                OnPropertyChanged(nameof(ConclusionSummary));
                OnPropertyChanged(nameof(ConclusionImplications));
                OnPropertyChanged(nameof(HasConclusion));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ConcludeAsync()
        {
            if (_aiService == null || _persistence == null)
                return;

            IsConcluding = true;
            try
            {
                var contacts = await _persistence.GetAllAsync<AIContact>();
                var contact = contacts.Values.FirstOrDefault(c => c.IsPrimaryAI) ?? contacts.Values.FirstOrDefault();
                if (contact == null)
                    return;

                _journal = await _journalService.GenerateConclusionAsync(_journalId, contact);
                await LoadAsync();
            }
            finally
            {
                IsConcluding = false;
            }
        }

        private static void OpenLinkedFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open linked file: {ex.Message}");
            }
        }
    }

    public class JournalPageViewModel
    {
        public JournalPageViewModel(JournalPageEntry entry)
        {
            Title = entry.Title;
            Body = entry.Body;
            DateLabel = entry.Timestamp.ToString("MMMM dd, yyyy · h:mm tt");
            KindLabel = FormatKind(entry.Kind);
            References = entry.References
                .Select(r => new ReferenceViewModel(r))
                .ToList();
            LinkedFiles = entry.LinkedFilePaths.ToList();
            HasReferences = References.Count > 0;
            HasLinkedFiles = LinkedFiles.Count > 0;
        }

        public string Title { get; }
        public string Body { get; }
        public string DateLabel { get; }
        public string KindLabel { get; }
        public List<ReferenceViewModel> References { get; }
        public List<string> LinkedFiles { get; }
        public bool HasReferences { get; }
        public bool HasLinkedFiles { get; }

        private static string FormatKind(JournalEntryKind kind) => kind switch
        {
            JournalEntryKind.Research => "Research",
            JournalEntryKind.ProjectWork => "Project Work",
            JournalEntryKind.Reflection => "Reflection",
            JournalEntryKind.Art => "Art",
            JournalEntryKind.Thought => "Thought",
            JournalEntryKind.Environment => "Environment",
            _ => "Entry"
        };
    }

    public class ReferenceViewModel
    {
        public ReferenceViewModel(ReferencedMaterial material)
        {
            Title = material.Title;
            Detail = material.Url ?? material.FilePath ?? material.Source ?? material.Notes ?? string.Empty;
        }

        public string Title { get; }
        public string Detail { get; }
    }
}
