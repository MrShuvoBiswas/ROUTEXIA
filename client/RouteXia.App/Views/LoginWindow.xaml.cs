using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;
using Wpf.Ui.Controls;

namespace RouteXia.App.Views
{
    public partial class LoginWindow : FluentWindow
    {
        private readonly AuthViewModel _vm;

        public LoginWindow()
        {
            InitializeComponent();
            _vm = App.Services.GetRequiredService<AuthViewModel>();
            DataContext = _vm;

            _vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AuthViewModel.IsAuthenticated) && _vm.IsAuthenticated)
                {
                    Dispatcher.Invoke(ProceedToMainWindow);
                }
            };
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.PasswordBox pb)
            {
                _vm.Password = pb.Password;
            }
        }

        private async void BtnBrowserLogin_Click(object sender, RoutedEventArgs e)
        {
            bool success = await _vm.SignInWithBrowserAsync();
            if (success)
            {
                ProceedToMainWindow();
            }
        }

        private async void BtnDirectSubmit_Click(object sender, RoutedEventArgs e)
        {
            bool success = await _vm.SubmitAuthAsync();
            if (success)
            {
                ProceedToMainWindow();
            }
        }

        private void BtnSwitchMode_Click(object sender, RoutedEventArgs e)
        {
            _vm.ToggleMode();
        }

        private void ProceedToMainWindow()
        {
            var mainWindow = App.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            this.Close();
        }
    }
}
