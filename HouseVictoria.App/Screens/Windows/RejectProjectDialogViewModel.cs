using HouseVictoria.App.HelperClasses;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public class RejectProjectDialogViewModel : ObservableObject
    {
        private string _reason = string.Empty;
        private string _suggestions = string.Empty;
        private double _priority = 7;
        private DateTime _newStartDate = DateTime.Now;
        private DateTime _newDeadline = DateTime.Now.AddDays(14);
        private string _validationError = string.Empty;

        public RejectProjectDialogViewModel(AfterActionReport report)
        {
            ProjectName = report?.ProjectName ?? string.Empty;
        }

        public string ProjectName { get; }

        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        public string Suggestions
        {
            get => _suggestions;
            set => SetProperty(ref _suggestions, value);
        }

        public double Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        public DateTime NewStartDate
        {
            get => _newStartDate;
            set => SetProperty(ref _newStartDate, value);
        }

        public DateTime NewDeadline
        {
            get => _newDeadline;
            set => SetProperty(ref _newDeadline, value);
        }

        public string ValidationError
        {
            get => _validationError;
            set => SetProperty(ref _validationError, value);
        }

        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(Reason))
            {
                ValidationError = "Please describe what was wrong with the project.";
                return false;
            }

            if (NewDeadline <= NewStartDate)
            {
                ValidationError = "The new deadline must be after the new start date.";
                return false;
            }

            ValidationError = string.Empty;
            return true;
        }

        public AarRejectionFeedback BuildFeedback() => new()
        {
            Reason = Reason.Trim(),
            Suggestions = Suggestions.Trim(),
            NewPriority = (int)Math.Round(Priority),
            NewStartDate = NewStartDate,
            NewDeadline = NewDeadline
        };
    }
}
