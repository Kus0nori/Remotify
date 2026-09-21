using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Remotify.Shared;
using Remotify.Shared.Models;

namespace Remotify.Service.Services;

public class IpcClientService : IDisposable
{
    private readonly ILogger<IpcClientService> _logger;
    private volatile bool _isConnected;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public bool IsConnected => _isConnected;

    public IpcClientService(ILogger<IpcClientService> logger)
    {
        _logger = logger;
    }

    public async Task<IpcResponse?> SendRequestAsync(string type, string? id = null)
    {
        await _connectionLock.WaitAsync();
        try
        {
            using var client = new NamedPipeClientStream(".", Constants.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            try
            {
                await client.ConnectAsync(1000);
            }
            catch (TimeoutException)
            {
                _isConnected = false;
                return null;
            }
            catch (IOException)
            {
                _isConnected = false;
                return null;
            }

            _isConnected = true;

            var request = new IpcRequest { Type = type, Id = id };
            var requestJson = JsonSerializer.Serialize(request);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);

            // Write length prefix (4 bytes) + data
            var lengthBytes = BitConverter.GetBytes(requestBytes.Length);
            await client.WriteAsync(lengthBytes);
            await client.WriteAsync(requestBytes);
            await client.FlushAsync();

            // Read response length
            var responseLengthBytes = new byte[4];
            var bytesRead = await client.ReadAsync(responseLengthBytes);
            if (bytesRead < 4)
            {
                return null;
            }

            var responseLength = BitConverter.ToInt32(responseLengthBytes);
            if (responseLength <= 0 || responseLength > 10 * 1024 * 1024) // Max 10MB
            {
                return null;
            }

            // Read response data
            var responseBytes = new byte[responseLength];
            var totalRead = 0;
            while (totalRead < responseLength)
            {
                var read = await client.ReadAsync(responseBytes.AsMemory(totalRead, responseLength - totalRead));
                if (read == 0)
                {
                    break;
                }
                totalRead += read;
            }

            var responseJson = Encoding.UTF8.GetString(responseBytes, 0, totalRead);
            return JsonSerializer.Deserialize<IpcResponse>(responseJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to communicate with WinUI app via IPC");
            _isConnected = false;
            return null;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", Constants.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(500);
            _isConnected = true;
            return true;
        }
        catch
        {
            _isConnected = false;
            return false;
        }
    }

    public void Dispose()
    {
        _connectionLock.Dispose();
    }
}
