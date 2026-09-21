namespace Remotify.Service.Services;

public class ServiceWorker : BackgroundService
{
    private readonly ILogger<ServiceWorker> _logger;
    private readonly IpcClientService _ipcClient;

    public ServiceWorker(ILogger<ServiceWorker> logger, IpcClientService ipcClient)
    {
        _logger = logger;
        _ipcClient = ipcClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Remotify Service started");

        // Periodically check connection to WinUI app
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _ipcClient.CheckConnectionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking IPC connection");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Remotify Service stopping");
        return base.StopAsync(cancellationToken);
    }
}
