using System;
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
                ViewModel = new CreateDataBankDialogViewModel(existingBank);
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

        public CreateDataBankDialogViewModel(DataBank? existingBank = null)
        {
            _existingBank = existingBank;
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
                    bank.CreatedAt = DateTime.Now;
                }

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
