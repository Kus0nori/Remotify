using Remotify.Service.Services;
using Remotify.Shared;
using Remotify.Shared.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = Constants.ServiceDisplayName;
});

builder.Services.AddSingleton<SharedSettingsService>();
builder.Services.AddSingleton<PowerService>();
builder.Services.AddSingleton<IpcClientService>();
builder.Services.AddHostedService<ServiceWorker>();

var settingsService = new SharedSettingsService();
settingsService.Load();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(settingsService.Settings.Port);
});

var app = builder.Build();

var ipcClient = app.Services.GetRequiredService<IpcClientService>();
var settings = app.Services.GetRequiredService<SharedSettingsService>();
settings.Load();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/ping"))
    {
        await next();
        return;
    }

    var authHeader = context.Request.Headers.Authorization.ToString();
    var expectedToken = $"Bearer {settings.Settings.AuthToken}";

    if (!string.Equals(authHeader, expectedToken, StringComparison.Ordinal))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
        return;
    }

    await next();
});

app.MapGet("/api/ping", () =>
{
    var sessionActive = ipcClient.IsConnected;
    return Results.Json(new
    {
        status = "ok",
        userSessionActive = sessionActive,
        features = new
        {
            commands = true,
            apps = sessionActive,
            metrics = sessionActive
        }
    });
});

app.MapPost("/api/command", async (HttpContext context) =>
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

    var powerService = app.Services.GetRequiredService<PowerService>();

    switch (request.Action.ToLowerInvariant())
    {
        case "shutdown":
            powerService.Shutdown();
            break;
        case "reboot":
            powerService.Reboot();
            break;
        case "sleep":
            powerService.Sleep();
            break;
        case "hibernate":
            powerService.Hibernate();
            break;
        default:
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Results.Json(new { error = $"Unknown action: {request.Action}" });
    }

    return Results.Json(new { success = true });
});

app.MapGet("/api/apps", async () =>
{
    if (!ipcClient.IsConnected)
    {
        return Results.Json(new { apps = Array.Empty<object>(), userSessionActive = false });
    }

    var response = await ipcClient.SendRequestAsync("apps");
    if (response?.Success == true && response.Data != null)
    {
        return Results.Json(new { apps = response.Data, userSessionActive = true });
    }

    return Results.Json(new { apps = Array.Empty<object>(), userSessionActive = false });
});

app.MapGet("/api/apps/{id}/icon", async (string id) =>
{
    if (!ipcClient.IsConnected)
    {
        return Results.NotFound(new { error = "User session not active" });
    }

    var response = await ipcClient.SendRequestAsync("app_icon", id);
    if (response?.Success == true && response.Data is string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        return Results.Bytes(bytes, "image/png");
    }

    return Results.NotFound(new { error = "Icon not found" });
});

app.MapGet("/api/metrics", async () =>
{
    if (!ipcClient.IsConnected)
    {
        return Results.Json(new { error = "User session not active", userSessionActive = false });
    }

    var response = await ipcClient.SendRequestAsync("metrics");
    if (response?.Success == true && response.Data != null)
    {
        return Results.Json(response.Data);
    }

    return Results.Json(new { error = "Failed to get metrics", userSessionActive = false });
});

app.Run();

internal record CommandRequest(string Action);
