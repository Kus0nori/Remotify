using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using Remotify.Shared;

namespace Remotify.Services;

public class ServiceInstallerService
{
    public bool IsInstalled
    {
        get
        {
            try
            {
                using var sc = new ServiceController(Constants.ServiceName);
                _ = sc.Status;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    public bool IsRunning
    {
        get
        {
            try
            {
                using var sc = new ServiceController(Constants.ServiceName);
                return sc.Status == ServiceControllerStatus.Running;
            }
            catch
            {
                return false;
            }
        }
    }

    public ServiceControllerStatus? Status
    {
        get
        {
            try
            {
                using var sc = new ServiceController(Constants.ServiceName);
                return sc.Status;
            }
            catch
            {
                return null;
            }
        }
    }

    public string GetServiceExePath()
    {
        // Service exe is copied to the same directory as the main app
        return Path.Combine(AppContext.BaseDirectory, "Remotify.Service.exe");
    }

    public async Task<bool> InstallAsync()
    {
        var serviceExePath = GetServiceExePath();

        if (!File.Exists(serviceExePath))
        {
            throw new FileNotFoundException($"Service executable not found: {serviceExePath}");
        }

        var script = $@"
            $serviceName = '{Constants.ServiceName}'
            $existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
            if ($existingService) {{
                Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
                sc.exe delete $serviceName
                Start-Sleep -Seconds 1
            }}
            New-Service -Name '{Constants.ServiceName}' `
                        -BinaryPathName '{serviceExePath}' `
                        -DisplayName '{Constants.ServiceDisplayName}' `
                        -Description '{Constants.ServiceDescription}' `
                        -StartupType Automatic
            Start-Service -Name '{Constants.ServiceName}'
        ";

        return await RunPowerShellAsAdminAsync(script);
    }

    public async Task<bool> UninstallAsync()
    {
        var script = $@"
            $serviceName = '{Constants.ServiceName}'
            $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
            if ($service) {{
                Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
                sc.exe delete $serviceName
            }}
        ";

        return await RunPowerShellAsAdminAsync(script);
    }

    public async Task<bool> StartAsync()
    {
        try
        {
            using var sc = new ServiceController(Constants.ServiceName);
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30)));
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StopAsync()
    {
        try
        {
            using var sc = new ServiceController(Constants.ServiceName);
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)));
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> RunPowerShellAsAdminAsync(string script)
    {
        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"remotify_install_{Guid.NewGuid():N}.ps1");

        try
        {
            await File.WriteAllTextAsync(tempScriptPath, script);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{tempScriptPath}\"",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = false
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled UAC prompt
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempScriptPath))
                {
                    File.Delete(tempScriptPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
