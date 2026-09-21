using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Remotify.Shared.Models;

namespace Remotify.Services;

public class MetricsService : IDisposable
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly Timer _sampleTimer;

    private long _lastNetworkBytesSent;
    private long _lastNetworkBytesReceived;
    private DateTime _lastSampleTime;

    private long _networkUploadBps;
    private long _networkDownloadBps;
    private float _cpuUsage;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public MetricsService()
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _cpuCounter.NextValue(); // Initial call returns 0

        (_lastNetworkBytesSent, _lastNetworkBytesReceived) = GetNetworkBytes();
        _lastSampleTime = DateTime.UtcNow;

        // Sample every second for accurate network speed
        _sampleTimer = new Timer(SampleMetrics, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void SampleMetrics(object? state)
    {
        try
        {
            _cpuUsage = _cpuCounter.NextValue();

            var (bytesSent, bytesReceived) = GetNetworkBytes();
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastSampleTime).TotalSeconds;

            if (elapsed > 0)
            {
                _networkUploadBps = (long)((bytesSent - _lastNetworkBytesSent) / elapsed);
                _networkDownloadBps = (long)((bytesReceived - _lastNetworkBytesReceived) / elapsed);
            }

            _lastNetworkBytesSent = bytesSent;
            _lastNetworkBytesReceived = bytesReceived;
            _lastSampleTime = now;
        }
        catch
        {
            // Ignore sampling errors
        }
    }

    public SystemMetrics GetMetrics()
    {
        var (ramUsage, ramUsedGb, ramTotalGb) = GetMemoryInfo();
        var uptimeSeconds = Environment.TickCount64 / 1000;

        return new SystemMetrics
        {
            CpuUsage = Math.Round(_cpuUsage, 1),
            RamUsage = Math.Round(ramUsage, 1),
            RamUsedGb = Math.Round(ramUsedGb, 2),
            RamTotalGb = Math.Round(ramTotalGb, 2),
            NetworkUploadBps = Math.Max(0, _networkUploadBps),
            NetworkDownloadBps = Math.Max(0, _networkDownloadBps),
            UptimeSeconds = uptimeSeconds
        };
    }

    private static (double usage, double usedGb, double totalGb) GetMemoryInfo()
    {
        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };

        if (GlobalMemoryStatusEx(ref memStatus))
        {
            var totalGb = memStatus.ullTotalPhys / (1024.0 * 1024 * 1024);
            var availGb = memStatus.ullAvailPhys / (1024.0 * 1024 * 1024);
            var usedGb = totalGb - availGb;
            var usage = (usedGb / totalGb) * 100;

            return (usage, usedGb, totalGb);
        }

        return (0, 0, 0);
    }

    private static (long sent, long received) GetNetworkBytes()
    {
        long totalSent = 0;
        long totalReceived = 0;

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var stats = ni.GetIPv4Statistics();
                totalSent += stats.BytesSent;
                totalReceived += stats.BytesReceived;
            }
        }
        catch
        {
            // Ignore network errors
        }

        return (totalSent, totalReceived);
    }

    public void Dispose()
    {
        _sampleTimer.Dispose();
        _cpuCounter.Dispose();
    }
}
