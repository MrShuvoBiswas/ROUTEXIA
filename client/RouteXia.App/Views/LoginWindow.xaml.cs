using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;
using Wpf.Ui.Controls;

namespace RouteXia.App.Views
{
    public partial class LoginWindow : FluentWindow
    {
        private readonly AuthViewModel _vm;
        private readonly SolidColorBrush _errorBorderBrush = new(Color.FromRgb(0xFF, 0x3B, 0x30));

        public LoginWindow()
        {
            InitializeComponent();
            _vm = App.Services.GetRequiredService<AuthViewModel>();
            _vm.IsRegisterMode = false; // Standard in-app login mode
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
                LblPasswordError.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            _vm.Email = TxtEmail.Text?.Trim() ?? string.Empty;
            _vm.Password = TxtPassword.Password ?? string.Empty;

            // Reset validation states
            LblEmailError.Visibility = Visibility.Collapsed;
            LblPasswordError.Visibility = Visibility.Collapsed;

            bool hasError = false;

            if (string.IsNullOrWhiteSpace(_vm.Email))
            {
                LblEmailError.Visibility = Visibility.Visible;
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(_vm.Password))
            {
                LblPasswordError.Visibility = Visibility.Visible;
                hasError = true;
            }

            if (hasError) return;

            BtnLogin.IsEnabled = false;
            BtnLogin.Content = "Signing In...";

            try
            {
                // Direct in-app API login — No browser redirect needed!
                _vm.IsRegisterMode = false;
                bool success = await _vm.SubmitAuthAsync();
                if (success)
                {
                    ProceedToMainWindow();
                }
            }
            finally
            {
                BtnLogin.IsEnabled = true;
                BtnLogin.Content = "Login";
            }
        }

        private async void BtnGoogleAuth_Click(object sender, RoutedEventArgs e)
        {
            // Google 1-click Auth (Redirects to Web Auth Portal)
            bool success = await _vm.SignInWithBrowserAsync();
            if (success)
            {
                ProceedToMainWindow();
            }
        }

        private async void LnkRegister_Click(object sender, MouseButtonEventArgs e)
        {
            // Registration & claiming 4 days trial opens the web auth portal
            bool success = await _vm.SignInWithBrowserAsync();
            if (success)
            {
                ProceedToMainWindow();
            }
        }

        private void LnkForgotPassword_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Open password reset portal in browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://3.1.31.201:8080/auth?mode=reset",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _vm.StatusMessage = $"Could not launch browser: {ex.Message}";
            }
        }

        private void ProceedToMainWindow()
        {
            var mainWindow = App.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            this.Close();
        }
    }
}
