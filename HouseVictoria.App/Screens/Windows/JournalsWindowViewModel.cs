using System.Collections.ObjectModel;
using System.Windows.Input;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public class JournalsWindowViewModel : ObservableObject
    {
        private readonly IJournalService _journalService;
        private bool _isLoading;
        private string _searchText = string.Empty;

        public ObservableCollection<JournalListItemViewModel> Journals { get; } = new();
        public ICommand RefreshCommand { get; }
        public ICommand OpenJournalCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    _ = LoadJournalsAsync();
            }
        }

        public JournalsWindowViewModel(IJournalService journalService)
        {
            _journalService = journalService ?? throw new ArgumentNullException(nameof(journalService));
            RefreshCommand = new RelayCommand(async () => await LoadJournalsAsync());
            OpenJournalCommand = new RelayCommand<string?>(OpenJournal);
            _journalService.JournalUpdated += (_, _) => _ = LoadJournalsAsync();
        }

        public async Task LoadJournalsAsync()
        {
            IsLoading = true;
            try
            {
                var all = await _journalService.GetAllJournalsAsync();
                var filtered = string.IsNullOrWhiteSpace(SearchText)
                    ? all
                    : all.Where(j =>
                        j.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        j.Topic.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        j.Preface.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

                Journals.Clear();
                foreach (var journal in filtered)
                {
                    Journals.Add(new JournalListItemViewModel(journal));
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OpenJournal(string? journalId)
        {
            if (string.IsNullOrWhiteSpace(journalId))
                return;

            var reader = new JournalReaderWindow(journalId);
            reader.Owner = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive) ?? reader.Owner;
            reader.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            reader.Show();
        }
    }

    public class JournalListItemViewModel
    {
        public JournalListItemViewModel(ResearchJournal journal)
        {
            Id = journal.Id;
            Title = journal.Title;
            Topic = journal.Topic;
            PrefacePreview = Truncate(journal.Preface, 120);
            Status = journal.Status;
            StatusLabel = journal.Status == JournalStatus.Concluded ? "Concluded" : "Active";
            EntryCount = journal.Entries.Count(e => e.Kind != JournalEntryKind.Conclusion);
            UpdatedAt = journal.UpdatedAt;
            UpdatedLabel = journal.UpdatedAt.ToString("MMM dd, yyyy");
            HasProject = !string.IsNullOrWhiteSpace(journal.ProjectName);
            ProjectName = journal.ProjectName ?? string.Empty;
        }

        public string Id { get; }
        public string Title { get; }
        public string Topic { get; }
        public string PrefacePreview { get; }
        public JournalStatus Status { get; }
        public string StatusLabel { get; }
        public int EntryCount { get; }
        public DateTime UpdatedAt { get; }
        public string UpdatedLabel { get; }
        public bool HasProject { get; }
        public string ProjectName { get; }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            return text.Length <= max ? text : text[..max].TrimEnd() + "…";
        }
    }
}
