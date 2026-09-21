using System.Security.Cryptography;
using System.Text.Json;
using Remotify.Shared.Models;

namespace Remotify.Shared.Services;

public class SharedSettingsService
{
    private readonly string _settingsPath;
    private AppSettings _settings = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Settings => _settings;

    public SharedSettingsService()
    {
        _settingsPath = Constants.SharedSettingsPath;
    }

    public void Load()
    {
        EnsureDirectoryExists();

        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
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

        if (string.IsNullOrEmpty(_settings.AuthToken))
        {
            _settings.AuthToken = GenerateToken();
            Save();
        }
    }

    public void Save()
    {
        EnsureDirectoryExists();
        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    public void RegenerateToken()
    {
        _settings.AuthToken = GenerateToken();
        Save();
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
