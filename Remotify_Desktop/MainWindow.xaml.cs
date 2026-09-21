using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left, Right, Top, Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_STYLE = -16;
    private const int WS_BORDER = 0x00800000;
    private const int WS_DLGFRAME = 0x00400000;
    private const int WS_THICKFRAME = 0x00040000;

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWCP_ROUND = 2;
    private const uint DWMWA_COLOR_NONE = 0xFFFFFFFE;
    private const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;

    private App AppInstance => App.Current;
    private bool _isInitializing = true;
    private AppWindow? _appWindow;

    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new DesktopAcrylicBackdrop();
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

        _appWindow.Resize(new SizeInt32(400, 580));
        _appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        var darkMode = 1;
        DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        var cornerPreference = DWMWCP_ROUND;
        DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

        uint borderColor = 0x00000000;
        DwmSetWindowAttribute(hWnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(uint));

        uint captionColor = 0x00000000;
        DwmSetWindowAttribute(hWnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(uint));

        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);

        var style = GetWindowLong(hWnd, GWL_STYLE);
        style &= ~(WS_BORDER | WS_DLGFRAME | WS_THICKFRAME);
        SetWindowLong(hWnd, GWL_STYLE, style);

        _appWindow.Closing += OnWindowClosing;
        Activated += OnWindowActivated;
    }

    public void ShowWithAnimation()
    {
        var hWnd = WindowNative.GetWindowHandle(this);

        uint borderColor = 0x00000000;
        DwmSetWindowAttribute(hWnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(uint));

        var displayArea = DisplayArea.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(hWnd),
            DisplayAreaFallback.Primary);

        if (displayArea == null || _appWindow == null) return;

        var workArea = displayArea.WorkArea;
        var finalX = workArea.X + workArea.Width - _appWindow.Size.Width - 12;
        var finalY = workArea.Y + workArea.Height - _appWindow.Size.Height - 12;

        var startY = workArea.Y + workArea.Height + 20;
        _appWindow.Move(new PointInt32(finalX, startY));

        Activate();
        SetForegroundWindow(hWnd);

        const int durationMs = 250;
        const int frameMs = 12;
        var totalFrames = durationMs / frameMs;
        var frame = 0;
        var distance = startY - finalY;

        var animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(frameMs) };
        animationTimer.Tick += (_, _) =>
        {
            frame++;
            var t = (double)frame / totalFrames;

            var eased = 1 - Math.Pow(1 - t, 3);
            var currentY = startY - (distance * eased);

            if (frame >= totalFrames)
            {
                currentY = finalY;
                animationTimer.Stop();
            }

            _appWindow.Move(new PointInt32(finalX, (int)currentY));
        };
        animationTimer.Start();
    }

    public void ForceFocus()
    {
        var hWnd = WindowNative.GetWindowHandle(this);
        SetForegroundWindow(hWnd);
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            App.Current.MinimizeToTray(this);
        }
    }

    private void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        App.Current.MinimizeToTray(this);
    }

    private void PositionNearTray()
    {
        if (_appWindow == null) return;

        var hWnd = WindowNative.GetWindowHandle(this);
        var displayArea = DisplayArea.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(hWnd),
            DisplayAreaFallback.Primary);

        if (displayArea != null)
        {
            var workArea = displayArea.WorkArea;
            var x = workArea.X + workArea.Width - _appWindow.Size.Width - 12;
            var y = workArea.Y + workArea.Height - _appWindow.Size.Height - 12;
            _appWindow.Move(new PointInt32(x, y));
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
                icon.Glyph = "\uE73E";
                icon.Foreground = new SolidColorBrush(Colors.Green);
            }
        }
        else
        {
            StatusCard.Header = "Сервер остановлен";
            StatusCard.Description = "Не удалось запустить";
            if (StatusCard.HeaderIcon is FontIcon icon)
            {
                icon.Glyph = "\uE711";
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

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            InfoMessage.IsOpen = false;
            timer.Stop();
        };
        timer.Start();
    }
}
