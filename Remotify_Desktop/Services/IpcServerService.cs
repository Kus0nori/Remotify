using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Remotify.Shared;
using Remotify.Shared.Models;

namespace Remotify.Services;

public class IpcServerService : IDisposable
{
    private readonly WindowService _windowService;
    private readonly MetricsService _metricsService;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public IpcServerService(WindowService windowService, MetricsService metricsService)
    {
        _windowService = windowService;
        _metricsService = metricsService;
    }

    public void Start()
    {
        if (_serverTask != null) return;

        _cts = new CancellationTokenSource();
        _serverTask = RunServerAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts == null) return;

        _cts.Cancel();

        if (_serverTask != null)
        {
            try
            {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                // Ignore timeout
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _cts.Dispose();
        _cts = null;
        _serverTask = null;
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    Constants.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);

                // Handle connection in background
                _ = HandleConnectionAsync(server, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Continue accepting connections even if one fails
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        try
        {
            // Read request length
            var lengthBytes = new byte[4];
            var bytesRead = await server.ReadAsync(lengthBytes, cancellationToken);
            if (bytesRead < 4) return;

            var requestLength = BitConverter.ToInt32(lengthBytes);
            if (requestLength <= 0 || requestLength > 1024 * 1024) return; // Max 1MB

            // Read request data
            var requestBytes = new byte[requestLength];
            var totalRead = 0;
            while (totalRead < requestLength)
            {
                var read = await server.ReadAsync(
                    requestBytes.AsMemory(totalRead, requestLength - totalRead),
                    cancellationToken);
                if (read == 0) break;
                totalRead += read;
            }

            var requestJson = Encoding.UTF8.GetString(requestBytes, 0, totalRead);
            var request = JsonSerializer.Deserialize<IpcRequest>(requestJson);

            if (request == null)
            {
                await SendResponseAsync(server, IpcResponse.Fail("Invalid request"), cancellationToken);
                return;
            }

            var response = HandleRequest(request);
            await SendResponseAsync(server, response, cancellationToken);
        }
        catch (Exception)
        {
            // Connection error, ignore
        }
        finally
        {
            if (server.IsConnected)
            {
                server.Disconnect();
            }
        }
    }

    private IpcResponse HandleRequest(IpcRequest request)
    {
        return request.Type.ToLowerInvariant() switch
        {
            "apps" => HandleAppsRequest(),
            "app_icon" => HandleIconRequest(request.Id),
            "metrics" => HandleMetricsRequest(),
            _ => IpcResponse.Fail($"Unknown request type: {request.Type}")
        };
    }

    private IpcResponse HandleAppsRequest()
    {
        try
        {
            var apps = _windowService.GetVisibleWindows();
            return IpcResponse.Ok(apps);
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail(ex.Message);
        }
    }

    private IpcResponse HandleIconRequest(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return IpcResponse.Fail("Icon ID is required");
        }

        try
        {
            var iconBytes = _windowService.GetAppIcon(id);
            if (iconBytes == null)
            {
                return IpcResponse.Fail("Icon not found");
            }

            var base64 = Convert.ToBase64String(iconBytes);
            return IpcResponse.Ok(base64);
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail(ex.Message);
        }
    }

    private IpcResponse HandleMetricsRequest()
    {
        try
        {
            var metrics = _metricsService.GetMetrics();
            return IpcResponse.Ok(metrics);
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail(ex.Message);
        }
    }

    private static async Task SendResponseAsync(NamedPipeServerStream server, IpcResponse response, CancellationToken cancellationToken)
    {
        var responseJson = JsonSerializer.Serialize(response, JsonOptions);
        var responseBytes = Encoding.UTF8.GetBytes(responseJson);
        var lengthBytes = BitConverter.GetBytes(responseBytes.Length);

        await server.WriteAsync(lengthBytes, cancellationToken);
        await server.WriteAsync(responseBytes, cancellationToken);
        await server.FlushAsync(cancellationToken);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
