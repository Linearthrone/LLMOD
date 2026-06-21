using System.Windows;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.App.Screens.Windows
{
    public partial class CreateDataBankDialog : Window
    {
        public CreateDataBankDialogViewModel ViewModel { get; } = null!;
        public DataBank? CreatedDataBank { get; private set; }

        public CreateDataBankDialog(DataBank? existingBank = null)
        {
            InitializeComponent();

            try
            {
                var memoryService = App.GetService<IMemoryService>()
                    ?? throw new InvalidOperationException("Memory service is not available.");
                ViewModel = new CreateDataBankDialogViewModel(existingBank, memoryService);
                DataContext = ViewModel;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in CreateDataBankDialog constructor: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
            {
                MessageBox.Show("Dialog failed to initialize.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var bank = await ViewModel.SaveDataBankAsync();
            if (bank != null)
            {
                CreatedDataBank = bank;
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class CreateDataBankDialogViewModel : HelperClasses.ObservableObject
    {
        private readonly DataBank? _existingBank;
        private readonly IMemoryService _memoryService;

        private string _name = string.Empty;
        private string? _description;
        private string? _validationError;

        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    Validate();
                }
            }
        }

        public string? Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string? ValidationError
        {
            get => _validationError;
            private set => SetProperty(ref _validationError, value);
        }

        public CreateDataBankDialogViewModel(DataBank? existingBank, IMemoryService memoryService)
        {
            _existingBank = existingBank;
            _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
            if (_existingBank != null)
            {
                _name = _existingBank.Name ?? string.Empty;
                _description = _existingBank.Description;
            }
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(_name))
            {
                ValidationError = "Name is required.";
            }
            else
            {
                ValidationError = null;
            }
        }

        public async Task<DataBank?> SaveDataBankAsync()
        {
            Validate();
            if (!string.IsNullOrWhiteSpace(ValidationError))
            {
                MessageBox.Show(ValidationError, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            try
            {
                var bank = _existingBank ?? new DataBank();
                bank.Name = _name.Trim();
                bank.Description = _description?.Trim();
                bank.LastModified = DateTime.Now;

                if (_existingBank == null)
                {
                    bank.Id = string.IsNullOrWhiteSpace(bank.Id) ? Guid.NewGuid().ToString() : bank.Id;
                    bank.CreatedAt = DateTime.Now;
                }

                await _memoryService.AddDataBankAsync(bank);
                return bank;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving data bank: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Error in SaveDataBankAsync: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
    }
}
