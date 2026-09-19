using System.Net;
using System.Net.Sockets;
using Microsoft.UI;
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

        // Set window size (450x400)
        _appWindow.Resize(new SizeInt32(450, 440));

        // Disable resizing and maximizing
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }

        // Center on screen
        CenterWindow();
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
        PortText.Text = settings.Port.ToString();
        TokenText.Text = settings.AuthToken;
        AutoStartCheckBox.IsChecked = AppInstance.StartupService.IsEnabled();
        FirewallCheckBox.IsChecked = settings.FirewallRuleEnabled;

        UpdateServerStatus();
    }

    private void UpdateServerStatus()
    {
        if (AppInstance.ApiServer.IsRunning)
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Green);
            StatusText.Text = "Сервер запущен";
        }
        else
        {
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusText.Text = "Сервер остановлен";
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
    }

    private void CopyToken_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard(TokenText.Text);
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
        }
    }

    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var enabled = AutoStartCheckBox.IsChecked == true;
        AppInstance.StartupService.SetEnabled(enabled);
    }

    private async void FirewallCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var enabled = FirewallCheckBox.IsChecked == true;
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
            FirewallCheckBox.IsChecked = !enabled;
            _isInitializing = false;

            await ShowMessageAsync(
                "Ошибка",
                "Не удалось изменить правило брандмауэра. Возможно, операция была отменена.");
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortText.Text, out var port) || port < 1 || port > 65535)
        {
            await ShowMessageAsync("Ошибка", "Порт должен быть числом от 1 до 65535.");
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

        await ShowMessageAsync("Remotify", "Настройки сохранены.");
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }
}
