using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RouteXia.VpnClient.Api;
using RouteXia.VpnClient.Security;

namespace RouteXia.App.ViewModels
{
    public class AuthViewModel : INotifyPropertyChanged
    {
        private readonly RouteXiaApiClient _apiClient;

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private bool _isError;
        public bool IsError
        {
            get => _isError;
            set { _isError = value; OnPropertyChanged(); }
        }

        private bool _isRegisterMode;
        public bool IsRegisterMode
        {
            get => _isRegisterMode;
            set { _isRegisterMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(ModeTitle)); OnPropertyChanged(nameof(SwitchButtonText)); }
        }

        public string ModeTitle => IsRegisterMode ? "Create an Account" : "Sign In to RouteXia";
        public string SwitchButtonText => IsRegisterMode ? "Already have an account? Sign In" : "New to RouteXia? Get 4 Days Free Trial";

        public bool IsAuthenticated => _apiClient.IsAuthenticated;
        public string UserEmail => _apiClient.CurrentUser?.Email ?? string.Empty;
        public string SubscriptionStatusText => _apiClient.CurrentSubscription?.Message ?? "No Active Subscription";
        public string PlanBadgeText => _apiClient.CurrentSubscription?.IsTrial == true ? "FREE TRIAL" : "PREMIUM";
        public string DaysLeftText => $"{_apiClient.CurrentSubscription?.DaysLeft ?? 0} Days Left";
        public string HwidPreview => HwidGenerator.GetHwid()[..16] + "...";

        public AuthViewModel(RouteXiaApiClient apiClient)
        {
            _apiClient = apiClient;
            _apiClient.AuthStateChanged += () =>
            {
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(UserEmail));
                OnPropertyChanged(nameof(SubscriptionStatusText));
                OnPropertyChanged(nameof(PlanBadgeText));
                OnPropertyChanged(nameof(DaysLeftText));
            };
        }

        public async Task<bool> SubmitAuthAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Please enter email and password";
                IsError = true;
                return false;
            }

            IsBusy = true;
            StatusMessage = "Connecting to server...";
            IsError = false;

            try
            {
                (bool success, string message) = IsRegisterMode
                    ? await _apiClient.RegisterAsync(Email, Password)
                    : await _apiClient.LoginAsync(Email, Password);

                StatusMessage = message;
                IsError = !success;
                return success;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void Logout()
        {
            _apiClient.Logout();
            StatusMessage = "Logged out successfully";
            IsError = false;
        }

        public void ToggleMode()
        {
            IsRegisterMode = !IsRegisterMode;
            StatusMessage = string.Empty;
            IsError = false;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
