using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RouteXia.App.Views;

public enum ModernPromptType
{
    Information,
    Subscription,
    Warning,
    Error,
    Banned
}

public partial class ModernPromptWindow : Window
{
    public bool Result { get; private set; }

    public ModernPromptWindow(
        string title,
        string message,
        ModernPromptType type = ModernPromptType.Information,
        string primaryBtnText = "OK",
        string? secondaryBtnText = null,
        string? category = null)
    {
        InitializeComponent();

        TxtTitle.Text = title.ToUpperInvariant();
        TxtMessage.Text = CleanMessage(message);
        TxtCategory.Text = category ?? GetDefaultCategory(type);
        TxtPrimaryBtn.Text = primaryBtnText.ToUpperInvariant();

        if (string.IsNullOrEmpty(secondaryBtnText))
        {
            BtnSecondary.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(BtnPrimary, 3);
        }
        else
        {
            BtnSecondary.Visibility = Visibility.Visible;
            TxtSecondaryBtn.Text = secondaryBtnText.ToUpperInvariant();
            Grid.SetColumnSpan(BtnPrimary, 1);
        }

        ApplyTypeStyle(type);
    }

    private void ApplyTypeStyle(ModernPromptType type)
    {
        switch (type)
        {
            case ModernPromptType.Subscription:
                TopGlowBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB020"));
                IconBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB020"));
                DialogIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Flash24;
                DialogIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB020"));
                break;

            case ModernPromptType.Banned:
            case ModernPromptType.Error:
                TopGlowBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4757"));
                IconBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4757"));
                DialogIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Shield24;
                DialogIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4757"));
                break;

            case ModernPromptType.Warning:
                TopGlowBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB020"));
                IconBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB020"));
                DialogIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Warning24;
                DialogIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB020"));
                break;

            case ModernPromptType.Information:
            default:
                TopGlowBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00C2FF"));
                IconBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00C2FF"));
                DialogIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Info24;
                DialogIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00C2FF"));
                break;
        }
    }

    private static string GetDefaultCategory(ModernPromptType type) => type switch
    {
        ModernPromptType.Subscription => "RouteXia Subscription",
        ModernPromptType.Banned => "Security & Account Status",
        ModernPromptType.Error => "System Notification",
        ModernPromptType.Warning => "Notice",
        _ => "Notification"
    };

    public static string CleanMessage(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return string.Empty;
        string trimmed = msg.Trim();
        if (trimmed.StartsWith("{") && trimmed.Contains("\"message\""))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("message", out var m))
                {
                    if (m.ValueKind == System.Text.Json.JsonValueKind.String)
                        return m.GetString() ?? msg;
                    if (m.ValueKind == System.Text.Json.JsonValueKind.Array && m.GetArrayLength() > 0)
                        return m[0].GetString() ?? msg;
                }
                if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return err.GetString() ?? msg;
                }
            }
            catch { }
        }
        return msg;
    }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }

    private void BtnSecondary_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }

    private void BtnPrimary_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    public static bool ShowPrompt(
        string title,
        string message,
        ModernPromptType type = ModernPromptType.Information,
        string primaryBtn = "OK",
        string? secondaryBtn = null,
        string? category = null)
    {
        return Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new ModernPromptWindow(title, message, type, primaryBtn, secondaryBtn, category);
            if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
                win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
            return win.Result;
        });
    }

    public static void ShowAlert(
        string title,
        string message,
        ModernPromptType type = ModernPromptType.Information,
        string okBtn = "I UNDERSTAND",
        string? category = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new ModernPromptWindow(title, message, type, okBtn, null, category);
            if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
                win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        });
    }
}
