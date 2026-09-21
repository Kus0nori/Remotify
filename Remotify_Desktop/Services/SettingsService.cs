using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Remotify.Shared;
using Remotify.Shared.Models;

namespace Remotify.Services;

public class SettingsService
{
    private readonly string _localSettingsPath;
    private readonly string _sharedSettingsPath;
    private AppSettings _settings = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Settings => _settings;

    public SettingsService()
    {
        var localAppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Remotify");

        Directory.CreateDirectory(localAppDataPath);
        _localSettingsPath = Path.Combine(localAppDataPath, "settings.json");
        _sharedSettingsPath = Constants.SharedSettingsPath;
    }

    public void Load()
    {
        // Load local settings first
        if (File.Exists(_localSettingsPath))
        {
            try
            {
                var json = File.ReadAllText(_localSettingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                _settings = new AppSettings();
            }
        }
        else
        {
            _settings = new AppSettings();
        }

        // Generate token if needed
        if (string.IsNullOrEmpty(_settings.AuthToken))
        {
            _settings.AuthToken = GenerateToken();
        }

        // Save to both locations
        Save();

        // Sync shared settings for service
        SyncSharedSettings();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        File.WriteAllText(_localSettingsPath, json);
    }

    public void SyncSharedSettings()
    {
        try
        {
            // Ensure shared directory exists
            var sharedDir = Path.GetDirectoryName(_sharedSettingsPath);
            if (!string.IsNullOrEmpty(sharedDir) && !Directory.Exists(sharedDir))
            {
                Directory.CreateDirectory(sharedDir);
            }

            // Write shared settings (only Port and AuthToken needed by service)
            var sharedSettings = new AppSettings
            {
                Port = _settings.Port,
                AuthToken = _settings.AuthToken,
                ServiceInstalled = _settings.ServiceInstalled
            };

            var json = JsonSerializer.Serialize(sharedSettings, JsonOptions);
            File.WriteAllText(_sharedSettingsPath, json);
        }
        catch
        {
            // Ignore errors writing to ProgramData (may need admin rights)
        }
    }

    public void RegenerateToken()
    {
        _settings.AuthToken = GenerateToken();
        Save();
        SyncSharedSettings();
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
