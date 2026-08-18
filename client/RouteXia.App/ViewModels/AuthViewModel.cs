using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RouteXia.VpnClient.Api;
using RouteXia.VpnClient.Auth;
using RouteXia.VpnClient.Security;

namespace RouteXia.App.ViewModels
{
    public class AuthViewModel : INotifyPropertyChanged
    {
        private readonly RouteXiaApiClient _apiClient;
        private readonly FirebaseAuthService _firebase;

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
            set
            {
                _isRegisterMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ModeTitle));
                OnPropertyChanged(nameof(SwitchButtonText));
            }
        }

        public string ModeTitle => IsRegisterMode ? "Create an Account" : "Sign In to RouteXia";
        public string SwitchButtonText => IsRegisterMode ? "Already have an account? Sign In" : "New to RouteXia? Get 4 Days Free Trial";

        public bool IsAuthenticated => _apiClient.IsAuthenticated;
        public string UserEmail => !string.IsNullOrEmpty(_apiClient.CurrentUser?.Email)
            ? _apiClient.CurrentUser.Email
            : string.Empty;

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
            _firebase  = new FirebaseAuthService();

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

        // ── Email / Password Login via Firebase ──────────────────────────────
        //
        // Flow:
        //   1. FirebaseAuthService signs in with email+password → gets Firebase idToken
        //   2. idToken sent to RouteXia backend /api/v1/auth/firebase
        //   3. Backend verifies token, returns RouteXia JWT + subscription data

        public async Task<bool> SubmitAuthAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Please enter email and password";
                IsError = true;
                return false;
            }

            IsBusy = true;
            IsError = false;
            StatusMessage = IsRegisterMode
                ? "Creating your account..."
                : "Signing in...";

            try
            {
                if (IsRegisterMode)
                {
                    // 1. Try Firebase register first
                    var fbRes = await _firebase.RegisterWithEmailPasswordAsync(Email, Password);
                    if (fbRes.Success && !string.IsNullOrEmpty(fbRes.IdToken))
                    {
                        var (fbSuccess, fbMsg) = await _apiClient.AuthenticateWithFirebaseTokenAsync(fbRes.IdToken, fbRes.Email);
                        StatusMessage = fbMsg;
                        IsError = !fbSuccess;
                        return fbSuccess;
                    }

                    // 2. Direct backend registration
                    var (regSuccess, regMsg) = await _apiClient.RegisterAsync(Email, Password);
                    StatusMessage = regMsg;
                    IsError = !regSuccess;
                    return regSuccess;
                }
                else
                {
                    // 1. Try Firebase sign-in (for users created via Firebase / Google)
                    var fbRes = await _firebase.SignInWithEmailPasswordAsync(Email, Password);
                    if (fbRes.Success && !string.IsNullOrEmpty(fbRes.IdToken))
                    {
                        StatusMessage = "Verifying with RouteXia server...";
                        var (fbSuccess, fbMsg) = await _apiClient.AuthenticateWithFirebaseTokenAsync(fbRes.IdToken, fbRes.Email);
                        StatusMessage = fbMsg;
                        IsError = !fbSuccess;
                        return fbSuccess;
                    }

                    // 2. Direct RouteXia DB sign-in (for users in PostgreSQL / Admin Panel)
                    var (dbSuccess, dbMsg) = await _apiClient.LoginAsync(Email, Password);
                    if (dbSuccess)
                    {
                        StatusMessage = dbMsg;
                        IsError = false;
                        return true;
                    }

                    // Show appropriate error message
                    StatusMessage = !string.IsNullOrWhiteSpace(fbRes.ErrorMessage) && fbRes.ErrorMessage != "No account found with this email address."
                        ? fbRes.ErrorMessage
                        : dbMsg;
                    IsError = true;
                    return false;
                }
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

        // ── Google OAuth via Browser ──────────────────────────────────────────
        //
        // Opens the web auth portal in a browser for Google sign-in.
        // The portal handles the Firebase Google OAuth flow and redirects
        // back with a Firebase idToken → backend verifies it.

        public async Task<bool> SignInWithGoogleAsync()
        {
            IsBusy = true;
            StatusMessage = "Opening Google sign-in in your browser...";
            IsError = false;

            try
            {
                FirebaseAuthResult result = await _firebase.SignInWithGoogleAsync(
                    authPortalUrl: RouteXia.VpnClient.Common.RouteXiaUrls.AuthPortalUrl);

                if (!result.Success || string.IsNullOrEmpty(result.IdToken))
                {
                    StatusMessage = result.ErrorMessage ?? "Google sign-in failed";
                    IsError = true;
                    return false;
                }

                StatusMessage = "Verifying with RouteXia server...";
                (bool success, string message) = await _apiClient.AuthenticateWithFirebaseTokenAsync(
                    result.IdToken, result.Email);

                StatusMessage = message;
                IsError = !success;
                return success;
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Google sign-in error: {ex.Message}";
                IsError = true;
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Kept for backward compatibility (maps to SignInWithGoogleAsync) ──
        public Task<bool> SignInWithBrowserAsync() => SignInWithGoogleAsync();

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
