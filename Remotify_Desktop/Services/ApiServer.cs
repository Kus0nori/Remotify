using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Remotify.Models;

namespace Remotify.Services;

public class ApiServer : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly PowerService _powerService;
    private WebApplication? _app;
    private CancellationTokenSource? _cts;

    public bool IsRunning => _app != null;

    public ApiServer(SettingsService settingsService, PowerService powerService)
    {
        _settingsService = settingsService;
        _powerService = powerService;
    }

    public async Task StartAsync()
    {
        if (_app != null) return;

        var settings = _settingsService.Settings;
        var builder = WebApplication.CreateSlimBuilder();

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(settings.Port);
        });

        _app = builder.Build();

        _app.Use(async (context, next) =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            var expectedToken = $"Bearer {_settingsService.Settings.AuthToken}";

            if (!string.Equals(authHeader, expectedToken, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
                return;
            }

            await next();
        });

        _app.MapPost("/api/command", async (HttpContext context) =>
        {
            CommandRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<CommandRequest>();
            }
            catch
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return Results.Json(new { error = "Invalid request body" });
            }

            if (request == null || string.IsNullOrEmpty(request.Action))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return Results.Json(new { error = "Action is required" });
            }

            switch (request.Action.ToLowerInvariant())
            {
                case "shutdown":
                    _powerService.Shutdown();
                    break;
                case "reboot":
                    _powerService.Reboot();
                    break;
                case "sleep":
                    _powerService.Sleep();
                    break;
                case "hibernate":
                    _powerService.Hibernate();
                    break;
                default:
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return Results.Json(new { error = $"Unknown action: {request.Action}" });
            }

            return Results.Json(new { success = true });
        });

        _app.MapGet("/api/ping", () => Results.Json(new { status = "ok" }));

        _cts = new CancellationTokenSource();
        _ = _app.RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_app == null) return;

        _cts?.Cancel();
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
        _cts?.Dispose();
        _cts = null;
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _app?.StopAsync().Wait();
        _app?.DisposeAsync().AsTask().Wait();
        _cts?.Dispose();
    }

    private record CommandRequest(string Action);
}
