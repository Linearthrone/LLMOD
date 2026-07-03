using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public class AfterActionReportWindowViewModel : ObservableObject
    {
        private readonly IAarService _aarService;
        private readonly Dispatcher _dispatcher;
        private bool _isLoading;
        private bool _reloadScheduled;

        public ObservableCollection<AarReportItemViewModel> Reports { get; } = new();

        public RelayCommand RefreshCommand { get; }
        public RelayCommand<string?> AcceptCommand { get; }
        public RelayCommand<string?> RejectCommand { get; }
        public RelayCommand<string?> OpenDeliverableCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool HasReports => Reports.Count > 0;

        public AfterActionReportWindowViewModel(IAarService aarService)
        {
            _aarService = aarService ?? throw new ArgumentNullException(nameof(aarService));
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            RefreshCommand = new RelayCommand(async () => await LoadReportsAsync());
            AcceptCommand = new RelayCommand<string?>(async id => await AcceptAsync(id));
            RejectCommand = new RelayCommand<string?>(async id => await RejectAsync(id));
            OpenDeliverableCommand = new RelayCommand<string?>(OpenDeliverable);

            _aarService.ReportsChanged += OnReportsChanged;
        }

        private void OnReportsChanged(object? sender, AarReportsChangedEventArgs e)
        {
            if (_reloadScheduled)
                return;

            _reloadScheduled = true;
            _dispatcher.BeginInvoke(DispatcherPriority.Background, () => _ = ReloadFromEventAsync());
        }

        private async Task ReloadFromEventAsync()
        {
            try
            {
                await LoadReportsAsync().ConfigureAwait(true);
            }
            finally
            {
                _reloadScheduled = false;
            }
        }

        public async Task LoadReportsAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            try
            {
                await _aarService.RefreshPendingDeliverablesAsync().ConfigureAwait(true);
                var pending = await _aarService.GetPendingReportsAsync().ConfigureAwait(true);
                Reports.Clear();
                foreach (var report in pending)
                    Reports.Add(new AarReportItemViewModel(report));
                OnPropertyChanged(nameof(HasReports));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AAR load failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AcceptAsync(string? reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId))
                return;

            try
            {
                await _aarService.AcceptAsync(reportId).ConfigureAwait(true);
                await LoadReportsAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AAR accept failed: {ex.Message}");
                MessageBox.Show($"Could not accept report: {ex.Message}", "After Action Report",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task RejectAsync(string? reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId))
                return;

            try
            {
                var report = await _aarService.GetReportAsync(reportId).ConfigureAwait(true);
                if (report == null)
                    return;

                var dialog = new RejectProjectDialog(report)
                {
                    Owner = Application.Current.Windows
                        .OfType<Window>()
                        .FirstOrDefault(w => w.IsActive),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                if (dialog.ShowDialog() == true && dialog.Feedback != null)
                {
                    await _aarService.RejectAsync(reportId, dialog.Feedback).ConfigureAwait(true);
                    await LoadReportsAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AAR reject failed: {ex.Message}");
                MessageBox.Show($"Could not reject report: {ex.Message}", "After Action Report",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenDeliverable(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if (!File.Exists(path))
                {
                    MessageBox.Show("The deliverable file could not be found.", "After Action Report",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AAR open deliverable failed: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            _aarService.ReportsChanged -= OnReportsChanged;
        }
    }

    public class AarReportItemViewModel
    {
        public AarReportItemViewModel(AfterActionReport report)
        {
            Id = report.Id;
            ProjectName = report.ProjectName;
            ProjectTypeLabel = report.ProjectType.ToString();
            Summary = report.Summary;
            Goal = report.Goal;
            Outcome = report.Outcome;

            CompletionPercentage = report.CompletionPercentage;
            CompletionPercentageLabel = $"{report.CompletionPercentage:F0}% complete";

            (CompletionLevelLabel, CompletionLevelColor) = report.CompletionLevel switch
            {
                AarCompletionLevel.Partial => ("Partially met", "#FFB74D"),
                AarCompletionLevel.Exceeded => ("Exceeded the goal", "#4DD0A0"),
                _ => ("Fully met", "#4DA6FF")
            };

            StartLabel = report.StartDate.ToString("MMM dd, yyyy");
            DeadlineLabel = report.Deadline.ToString("MMM dd, yyyy");
            CompletedLabel = report.CompletedAt.ToString("MMM dd, yyyy");
            TimeInvestedLabel = report.TimeInvestedLabel;
            WorkSessionLabel = $"{report.WorkSessionCount} work session(s)";
            OnTimeLabel = report.WasOnTime ? "On time" : "Past deadline";
            OnTimeColor = report.WasOnTime ? "#4DD0A0" : "#FF8A80";

            var paths = report.DeliverablePaths
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (paths.Count == 0 && !string.IsNullOrWhiteSpace(report.DeliverablePath) && File.Exists(report.DeliverablePath))
                paths.Add(report.DeliverablePath);

            HasDeliverable = paths.Count > 0;
            DeliverablePath = paths.FirstOrDefault() ?? string.Empty;
            DeliverableName = string.IsNullOrWhiteSpace(report.DeliverableName)
                ? (paths.Count > 0 ? Path.GetFileName(paths[0]) : "Open deliverable")
                : report.DeliverableName;
            DeliverableItems = paths
                .Select(p => new AarDeliverableItemViewModel(Path.GetFileName(p), p))
                .ToList();
            HasMultipleDeliverables = DeliverableItems.Count > 1;

            WorkExcerpt = report.WorkExcerpt ?? string.Empty;
            HasWorkExcerpt = !string.IsNullOrWhiteSpace(WorkExcerpt);
        }

        public string Id { get; }
        public string ProjectName { get; }
        public string ProjectTypeLabel { get; }
        public string Summary { get; }
        public string Goal { get; }
        public string Outcome { get; }

        public double CompletionPercentage { get; }
        public string CompletionPercentageLabel { get; }
        public string CompletionLevelLabel { get; }
        public string CompletionLevelColor { get; }

        public string StartLabel { get; }
        public string DeadlineLabel { get; }
        public string CompletedLabel { get; }
        public string TimeInvestedLabel { get; }
        public string WorkSessionLabel { get; }
        public string OnTimeLabel { get; }
        public string OnTimeColor { get; }

        public bool HasDeliverable { get; }
        public string DeliverableName { get; }
        public string DeliverablePath { get; }
        public IReadOnlyList<AarDeliverableItemViewModel> DeliverableItems { get; }
        public bool HasMultipleDeliverables { get; }
        public string WorkExcerpt { get; }
        public bool HasWorkExcerpt { get; }
    }

    public sealed class AarDeliverableItemViewModel
    {
        public AarDeliverableItemViewModel(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }
        public string Path { get; }
    }
}
