using System.Net;
using System.Net.Sockets;
using System.Windows;

namespace Remotify;

public partial class MainWindow : Window
{
    private App AppInstance => (App)Application.Current;
    private bool _isInitializing = true;

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
        _isInitializing = false;
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
            StatusIndicator.Fill = System.Windows.Media.Brushes.Green;
            StatusText.Text = "Сервер запущен";
        }
        else
        {
            StatusIndicator.Fill = System.Windows.Media.Brushes.Red;
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
        Clipboard.SetText(IpAddressText.Text);
    }

    private void CopyToken_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(TokenText.Text);
    }

    private void RegenerateToken_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Сгенерировать новый токен? Старый токен перестанет работать.",
            "Подтверждение",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
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

    private void FirewallCheckBox_Changed(object sender, RoutedEventArgs e)
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

            MessageBox.Show(
                "Не удалось изменить правило брандмауэра. Возможно, операция была отменена.",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortText.Text, out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show("Порт должен быть числом от 1 до 65535.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        MessageBox.Show("Настройки сохранены.", "Remotify", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}