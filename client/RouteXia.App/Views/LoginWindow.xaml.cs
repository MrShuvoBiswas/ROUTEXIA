using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;
using Wpf.Ui.Controls;

namespace RouteXia.App.Views
{
    public partial class LoginWindow : FluentWindow
    {
        private readonly AuthViewModel _vm;
        private Storyboard? _spinnerStoryboard;

        public LoginWindow()
        {
            InitializeComponent();
            _vm = App.Services.GetRequiredService<AuthViewModel>();
            _vm.IsRegisterMode = false; // Standard in-app login mode
            DataContext = _vm;

            _spinnerStoryboard = Resources.Contains("LoginSpinnerAnimation")
                ? (Storyboard)Resources["LoginSpinnerAnimation"]
                : null;

            _vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AuthViewModel.IsAuthenticated) && _vm.IsAuthenticated)
                {
                    Dispatcher.Invoke(ProceedToMainWindow);
                }
            };
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private bool _isClosingAnimated;
        private bool _isSwitchingToMainWindow;

        private void LoginWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isSwitchingToMainWindow)
            {
                return; // Closing login window to display MainWindow — do NOT shutdown app
            }

            if (!_isClosingAnimated)
            {
                e.Cancel = true;
                AnimateAndClose();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            AnimateAndClose();
        }

        private async void AnimateAndClose()
        {
            if (_isClosingAnimated || _isSwitchingToMainWindow) return;
            _isClosingAnimated = true;

            if (ClosingOverlay != null)
            {
                ClosingOverlay.Visibility = Visibility.Visible;

                // 1. Fade in the Industry Standard Closing Overlay (0 -> 1)
                var overlayFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(150));
                ClosingOverlay.BeginAnimation(UIElement.OpacityProperty, overlayFade);

                // 2. Start smooth 360 degree spin on center circular ring
                if (ClosingSpinnerTransform != null)
                {
                    var spinAnim = new DoubleAnimation
                    {
                        From = 0,
                        To = 360,
                        Duration = TimeSpan.FromMilliseconds(800),
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    ClosingSpinnerTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, spinAnim);
                }
            }

            // Allow industry standard spinning closing feedback while cleaning network resources
            await System.Threading.Tasks.Task.Delay(450);

            // 3. Smooth fade out window and perform full process exit
            var windowFade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150));
            windowFade.Completed += (_, _) =>
            {
                if (!_isSwitchingToMainWindow)
                {
                    App.PerformFullShutdown();
                }
            };
            this.BeginAnimation(Window.OpacityProperty, windowFade);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnLogin_Click(BtnLogin, new RoutedEventArgs());
            }
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

            // Start smooth loading spinner state
            BtnLogin.IsEnabled = false;
            LoginNormalState.Visibility = Visibility.Collapsed;
            LoginLoadingState.Visibility = Visibility.Visible;
            _spinnerStoryboard?.Begin();

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
                _spinnerStoryboard?.Stop();
                LoginLoadingState.Visibility = Visibility.Collapsed;
                LoginNormalState.Visibility = Visibility.Visible;
                BtnLogin.IsEnabled = true;
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
            _isSwitchingToMainWindow = true;
            var mainWindow = App.Services.GetRequiredService<MainWindow>();
            if (Application.Current != null)
            {
                Application.Current.MainWindow = mainWindow;
            }
            mainWindow.Show();
            this.Close();
        }
    }
}
