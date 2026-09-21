using System.Net;
using System.Net.Sockets;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using WinRT.Interop;

namespace Remotify;

public partial class MainWindow : Window
{
    private App AppInstance => App.Current;
    private bool _isInitializing = true;
    private AppWindow? _appWindow;

    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ConfigureWindow();
        LoadSettings();
        _isInitializing = false;
    }

    private void ConfigureWindow()
    {
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow == null) return;

        _appWindow.Resize(new SizeInt32(500, 700));
        _appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        _appWindow.Closing += OnWindowClosing;

        CenterWindow();
    }

    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        App.Current.MinimizeToTray(this);
    }

    private void CenterWindow()
    {
        if (_appWindow == null) return;

        var hWnd = WindowNative.GetWindowHandle(this);
        var displayArea = DisplayArea.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(hWnd),
            DisplayAreaFallback.Primary);

        if (displayArea != null)
        {
            var centerX = (displayArea.WorkArea.Width - _appWindow.Size.Width) / 2;
            var centerY = (displayArea.WorkArea.Height - _appWindow.Size.Height) / 2;
            _appWindow.Move(new PointInt32(centerX, centerY));
        }
    }

    private void LoadSettings()
    {
        var settings = AppInstance.SettingsService.Settings;

        IpAddressText.Text = GetLocalIpAddress();
        PortNumberBox.Value = settings.Port;
        TokenText.Text = settings.AuthToken;
        AutoStartToggle.IsOn = AppInstance.StartupService.IsEnabled();
        FirewallToggle.IsOn = settings.FirewallRuleEnabled;

        UpdateServerStatus();
    }

    private void UpdateServerStatus()
    {
        if (AppInstance.ApiServer.IsRunning)
        {
            StatusCard.Header = "Сервер запущен";
            StatusCard.Description = $"Порт {AppInstance.SettingsService.Settings.Port}";
            if (StatusCard.HeaderIcon is FontIcon icon)
            {
                icon.Glyph = "\uE73E"; // Checkmark
                icon.Foreground = new SolidColorBrush(Colors.Green);
            }
        }
        else
        {
            StatusCard.Header = "Сервер остановлен";
            StatusCard.Description = "Не удалось запустить";
            if (StatusCard.HeaderIcon is FontIcon icon)
            {
                icon.Glyph = "\uE711"; // Error
                icon.Foreground = new SolidColorBrush(Colors.Red);
            }
        }
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                return endPoint.Address.ToString();
            }
        }
        catch
        {
            // Fallback
        }

        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }

    private void CopyIp_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard(IpAddressText.Text);
        ShowInfoMessage("IP-адрес скопирован", InfoBarSeverity.Success);
    }

    private void CopyToken_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard(TokenText.Text);
        ShowInfoMessage("Токен скопирован", InfoBarSeverity.Success);
    }

    private static void CopyToClipboard(string text)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }

    private async void RegenerateToken_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Подтверждение",
            Content = "Сгенерировать новый токен? Старый токен перестанет работать.",
            PrimaryButtonText = "Да",
            CloseButtonText = "Нет",
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            AppInstance.SettingsService.RegenerateToken();
            TokenText.Text = AppInstance.SettingsService.Settings.AuthToken;
            ShowInfoMessage("Новый токен сгенерирован", InfoBarSeverity.Success);
        }
    }

    private void AutoStartToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var enabled = AutoStartToggle.IsOn;
        AppInstance.StartupService.SetEnabled(enabled);
    }

    private async void FirewallToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var enabled = FirewallToggle.IsOn;
        var settings = AppInstance.SettingsService.Settings;

        bool success;
        if (enabled)
        {
            success = AppInstance.FirewallService.AddRule(settings.Port);
        }
        else
        {
            success = AppInstance.FirewallService.RemoveRule();
        }

        if (success)
        {
            settings.FirewallRuleEnabled = enabled;
            AppInstance.SettingsService.Save();
        }
        else
        {
            _isInitializing = true;
            FirewallToggle.IsOn = !enabled;
            _isInitializing = false;

            ShowInfoMessage("Не удалось изменить правило брандмауэра", InfoBarSeverity.Error);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var port = (int)PortNumberBox.Value;

        if (port < 1 || port > 65535)
        {
            ShowInfoMessage("Порт должен быть от 1 до 65535", InfoBarSeverity.Error);
            return;
        }

        var settings = AppInstance.SettingsService.Settings;
        var portChanged = settings.Port != port;

        settings.Port = port;
        AppInstance.SettingsService.Save();

        if (portChanged)
        {
            if (settings.FirewallRuleEnabled)
            {
                AppInstance.FirewallService.UpdateRule(port);
            }

            await AppInstance.ApiServer.RestartAsync();
            UpdateServerStatus();
        }

        ShowInfoMessage("Настройки сохранены", InfoBarSeverity.Success);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowInfoMessage(string message, InfoBarSeverity severity)
    {
        InfoMessage.Message = message;
        InfoMessage.Severity = severity;
        InfoMessage.IsOpen = true;

        // Auto-hide after 3 seconds
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            InfoMessage.IsOpen = false;
            timer.Stop();
        };
        timer.Start();
    }
}
