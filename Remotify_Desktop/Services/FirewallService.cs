using System.Diagnostics;

namespace Remotify.Services;

public class FirewallService
{
    private const string RuleName = "Remotify";

    public bool AddRule(int port)
    {
        var args = $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow protocol=TCP localport={port}";
        return RunNetshAsAdmin(args);
    }

    public bool RemoveRule()
    {
        var args = $"advfirewall firewall delete rule name=\"{RuleName}\"";
        return RunNetshAsAdmin(args);
    }

    public bool UpdateRule(int newPort)
    {
        RemoveRule();
        return AddRule(newPort);
    }

    public bool RuleExists()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall show rule name=\"{RuleName}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 && output.Contains(RuleName);
        }
        catch
        {
            return false;
        }
    }

    private static bool RunNetshAsAdmin(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
