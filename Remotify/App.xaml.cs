using System.Threading;
using System.Windows;
using H.NotifyIcon;
using Remotify.Services;

namespace Remotify;

public partial class App : Application
{
    private static Mutex? _mutex;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;

    public SettingsService SettingsService { get; } = new();
    public PowerService PowerService { get; } = new();
    public StartupService StartupService { get; } = new();
    public FirewallService FirewallService { get; } = new();
    public ApiServer ApiServer { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "Remotify_SingleInstance";
        _mutex = new Mutex(true, mutexName, out var isNewInstance);

        if (!isNewInstance)
        {
            MessageBox.Show("Remotify уже запущен.", "Remotify", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        SettingsService.Load();
        ApiServer = new ApiServer(SettingsService, PowerService);

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.ForceCreate();

        await ApiServer.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        ApiServer?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            _mainWindow = new MainWindow();
        }

        _mainWindow.Show();
        _mainWindow.Activate();

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Shutdown();
    }

    private void TrayIcon_TrayLeftMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }
}