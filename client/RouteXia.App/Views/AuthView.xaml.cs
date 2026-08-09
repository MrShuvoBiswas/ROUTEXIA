using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RouteXia.App.ViewModels;

namespace RouteXia.App.Views
{
    public partial class AuthView : Page
    {
        private AuthViewModel _vm = null!;

        public AuthView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _vm = App.Services.GetRequiredService<AuthViewModel>();
            DataContext = _vm;

            UpdateViewVisibility();
            _vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AuthViewModel.IsAuthenticated))
                {
                    Dispatcher.Invoke(UpdateViewVisibility);
                }
            };
        }

        private void UpdateViewVisibility()
        {
            if (_vm.IsAuthenticated)
            {
                PanelProfile.Visibility = Visibility.Visible;
                PanelAuth.Visibility = Visibility.Collapsed;
            }
            else
            {
                PanelProfile.Visibility = Visibility.Collapsed;
                PanelAuth.Visibility = Visibility.Visible;
            }
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.PasswordBox pb)
            {
                _vm.Password = pb.Password;
            }
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            bool success = await _vm.SubmitAuthAsync();
            if (success)
            {
                UpdateViewVisibility();
            }
        }

        private void BtnSwitchMode_Click(object sender, RoutedEventArgs e)
        {
            _vm.ToggleMode();
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _vm.Logout();
            TxtPassword.Password = string.Empty;
            UpdateViewVisibility();
        }
    }
}
