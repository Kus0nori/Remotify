using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using H.NotifyIcon;
using Remotify.Services;

namespace Remotify;

public partial class App : Application
{
    private static Mutex? _mutex;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;

    public static new App Current => (App)Application.Current;

    public SettingsService SettingsService { get; } = new();
    public PowerService PowerService { get; } = new();
    public StartupService StartupService { get; } = new();
    public FirewallService FirewallService { get; } = new();
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

        SettingsService.Load();
        ApiServer = new ApiServer(SettingsService, PowerService);

        CreateTrayIcon();

        ApiServer.Start();
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Remotify"
        };

        var contextMenu = new MenuFlyout();

        var openItem = new MenuFlyoutItem
        {
            Text = "Открыть",
            FontWeight = Microsoft.UI.Text.FontWeights.Bold
        };
        openItem.Click += (_, _) => ShowMainWindow();
        contextMenu.Items.Add(openItem);

        contextMenu.Items.Add(new MenuFlyoutSeparator());

        var exitItem = new MenuFlyoutItem { Text = "Выход" };
        exitItem.Click += (_, _) => Shutdown();
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextFlyout = contextMenu;
        _trayIcon.LeftClickCommand = new RelayCommand(ShowMainWindow);
        _trayIcon.DoubleClickCommand = new RelayCommand(ShowMainWindow);
        _trayIcon.ForceCreate();
    }

    public void Shutdown()
    {
        _trayIcon?.Dispose();
        ApiServer?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        Exit();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow();
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }

        _mainWindow.Activate();
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
