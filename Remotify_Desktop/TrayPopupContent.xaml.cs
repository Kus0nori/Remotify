using System.Net;
using System.Net.Sockets;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace Remotify;

public sealed partial class TrayPopupContent : UserControl
{
    private App AppInstance => App.Current;

    public TrayPopupContent()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadData();
    }

    public void LoadData()
    {
        var settings = AppInstance.SettingsService.Settings;

        IpAddressText.Text = GetLocalIpAddress();
        TokenText.Text = settings.AuthToken;

        if (AppInstance.ApiServer.IsRunning)
        {
            StatusText.Text = "Сервер запущен";
            StatusDescription.Text = $"Порт {settings.Port}";
            StatusIcon.Glyph = "\uE73E";
            StatusIcon.Foreground = new SolidColorBrush(Colors.Green);
        }
        else
        {
            StatusText.Text = "Сервер остановлен";
            StatusDescription.Text = "Ошибка запуска";
            StatusIcon.Glyph = "\uE711";
            StatusIcon.Foreground = new SolidColorBrush(Colors.Red);
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

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        AppInstance.ShowMainWindow();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        AppInstance.Shutdown();
    }
}
