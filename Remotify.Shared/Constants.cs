namespace Remotify.Shared;

public static class Constants
{
    public const int DefaultPort = 5123;
    public const string PipeName = "Remotify_IPC";
    public const string ServiceName = "RemotifyService";
    public const string ServiceDisplayName = "Remotify Service";
    public const string ServiceDescription = "Allows remote shutdown and control before user login";

    public static string SharedSettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Remotify",
            "settings.json");

    public static string SharedSettingsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Remotify");
}
