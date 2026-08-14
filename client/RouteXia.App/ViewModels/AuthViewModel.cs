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
        public string UserEmail => !string.IsNullOrEmpty(_apiClient.CurrentUser?.Email) 
            ? _apiClient.CurrentUser.Email 
            : "sbiswas492003@gmail.com";

        public string CountryName => "India";
        public string CountryFlag => "🇮🇳";
        public string CountryDisplay => $"{CountryFlag}  {CountryName}";
        public string InternetProvider => "Zita Telecom Private Limited";

        public bool HasSubscription => _apiClient.CurrentSubscription?.CanConnect == true && _apiClient.CurrentSubscription?.DaysLeft > 0;
        public string SubscriptionTitle => HasSubscription ? "Active Pro Plan" : "No subscription";
        public string SubscriptionSubtitle => HasSubscription 
            ? $"Your plan is active ({_apiClient.CurrentSubscription?.DaysLeft} days remaining)." 
            : "You don't have an active plan.";

        public string SubscriptionStatusText => _apiClient.CurrentSubscription?.Message ?? "No Active Subscription";
        public string PlanBadgeText => _apiClient.CurrentSubscription?.IsTrial == true ? "FREE TRIAL" : "PREMIUM";
        public string DaysLeftText => $"{_apiClient.CurrentSubscription?.DaysLeft ?? 0} Days Left";
        public bool IsExpiryWarning => HasSubscription && (_apiClient.CurrentSubscription?.DaysLeft <= 7);
        public string HwidPreview => HwidGenerator.GetHwid()[..16] + "...";

        public AuthViewModel(RouteXiaApiClient apiClient)
        {
            _apiClient = apiClient;
            _apiClient.AuthStateChanged += () =>
            {
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(UserEmail));
                OnPropertyChanged(nameof(HasSubscription));
                OnPropertyChanged(nameof(SubscriptionTitle));
                OnPropertyChanged(nameof(SubscriptionSubtitle));
                OnPropertyChanged(nameof(SubscriptionStatusText));
                OnPropertyChanged(nameof(PlanBadgeText));
                OnPropertyChanged(nameof(DaysLeftText));
                OnPropertyChanged(nameof(IsExpiryWarning));
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

        public async Task<bool> SignInWithBrowserAsync()
        {
            IsBusy = true;
            StatusMessage = "Opening browser for Firebase authentication...";
            IsError = false;

            try
            {
                var browserAuth = new RouteXia.VpnClient.Auth.BrowserAuthService();
                var result = await browserAuth.StartBrowserAuthAsync();

                if (!result.Success || string.IsNullOrEmpty(result.Token))
                {
                    StatusMessage = result.ErrorMessage ?? "Browser authentication failed";
                    IsError = true;
                    return false;
                }

                StatusMessage = "Verifying with server...";
                (bool success, string message) = await _apiClient.AuthenticateWithFirebaseTokenAsync(result.Token, result.Email);

                StatusMessage = message;
                IsError = !success;
                return success;
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Authentication error: {ex.Message}";
                IsError = true;
                return false;
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
