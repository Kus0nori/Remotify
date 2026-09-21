using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Remotify.Shared.Services;

public class PowerService
{
    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public void Shutdown()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = "/s /t 0",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    public void Reboot()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown",
            Arguments = "/r /t 0",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    public void Sleep()
    {
        SetSuspendState(false, false, false);
    }

    public void Hibernate()
    {
        SetSuspendState(true, false, false);
    }
}
