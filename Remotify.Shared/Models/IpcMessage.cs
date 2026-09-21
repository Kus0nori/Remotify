using System.Text.Json.Serialization;

namespace Remotify.Shared.Models;

public class IpcRequest
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

public class IpcResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public object? Data { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    public static IpcResponse Ok(object? data = null) => new() { Success = true, Data = data };
    public static IpcResponse Fail(string error) => new() { Success = false, Error = error };
}

public class IpcAppsResponse
{
    [JsonPropertyName("apps")]
    public required AppInfo[] Apps { get; init; }
}

public class IpcIconResponse
{
    [JsonPropertyName("iconBase64")]
    public required string IconBase64 { get; init; }
}
