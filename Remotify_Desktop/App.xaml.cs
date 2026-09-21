using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using H.NotifyIcon;
using Remotify.Services;
using WinRT.Interop;

namespace Remotify;

public partial class App : Application
{
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    private const int SW_HIDE = 0;

    private static Mutex? _mutex;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private Window? _backgroundWindow;
    private bool _hasShownMinimizeNotification;

    public static new App Current => (App)Application.Current;

    public SettingsService SettingsService { get; } = new();
    public PowerService PowerService { get; } = new();
    public StartupService StartupService { get; } = new();
    public FirewallService FirewallService { get; } = new();
    public WindowService WindowService { get; } = new();
    public MetricsService MetricsService { get; } = new();
    public ApiServer ApiServer { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        const string mutexName = "Remotify_SingleInstance";
        _mutex = new Mutex(true, mutexName, out var isNewInstance);

        if (!isNewInstance)
        {
            // Can't show dialog without a window, just exit
            _mutex?.Dispose();
            Exit();
            return;
        }

        // Скрытое окно для поддержания процесса (WinUI 3 закрывается без окон)
        _backgroundWindow = new Window { Title = "Remotify" };
        var hwnd = WindowNative.GetWindowHandle(_backgroundWindow);
        ShowWindow(hwnd, SW_HIDE);

        SettingsService.Load();
        ApiServer = new ApiServer(SettingsService, PowerService, WindowService, MetricsService);

        CreateTrayIcon();

        ApiServer.Start();

        // Показываем окно при первом запуске (но не при автозапуске с Windows)
        if (!IsAutoStartLaunch() && !SettingsService.Settings.FirstRunCompleted)
        {
            ShowMainWindow();
            SettingsService.Settings.FirstRunCompleted = true;
            SettingsService.Save();
        }
    }

    private static bool IsAutoStartLaunch()
    {
        var args = Environment.GetCommandLineArgs();
        return args.Contains("--autostart", StringComparer.OrdinalIgnoreCase);
    }

    private void CreateTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Remotify",
            Icon = new Icon(iconPath)
        };

        var contextMenu = new MenuFlyout();

        var openItem = new MenuFlyoutItem
        {
            Text = "Открыть",
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Command = new RelayCommand(ShowMainWindow)
        };
        contextMenu.Items.Add(openItem);

        contextMenu.Items.Add(new MenuFlyoutSeparator());

        var exitItem = new MenuFlyoutItem
        {
            Text = "Выход",
            Command = new RelayCommand(Shutdown)
        };
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextFlyout = contextMenu;
        _trayIcon.LeftClickCommand = new RelayCommand(ShowMainWindow);
        _trayIcon.DoubleClickCommand = new RelayCommand(ShowMainWindow);
        _trayIcon.ForceCreate();
    }

    public void Shutdown()
    {
        try
        {
            _trayIcon?.Dispose();
            ApiServer?.Dispose();
            MetricsService?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        finally
        {
            Environment.Exit(0);
        }
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow();
        }

        _mainWindow.Activate();
    }

    public void MinimizeToTray(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        ShowWindow(hwnd, SW_HIDE);
        _mainWindow = null;

        if (!_hasShownMinimizeNotification)
        {
            _trayIcon?.TrayIcon.ShowNotification(
                "Remotify",
                "Remotify свёрнуто в системный трей",
                H.NotifyIcon.Core.NotificationIcon.Info);
            _hasShownMinimizeNotification = true;
        }
    }

    private class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) => _execute = execute;

#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
