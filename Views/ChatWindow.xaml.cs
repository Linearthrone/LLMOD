using System.Windows;
using LLMOD.Helpers;

namespace LLMOD.Views
{
    public partial class ChatWindow : Window
    {
        public ChatWindow()
        {
            InitializeComponent();
        }

        private void ChatWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Position: Right 33%, Full Height
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double screenThird = screenWidth / 3;

            this.Width = screenThird;
            this.Height = screenHeight;
            this.Left = screenWidth - this.Width;
            this.Top = 0;

            // Get Handle for Interop
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            WindowHelper.SetClickThrough(helper.Handle, false);
        }

        // Minimize logic (Hide on Deactivate)
        private void ChatWindow_Deactivated(object sender, System.EventArgs e)
        {
            this.Hide();
        }
    }
}