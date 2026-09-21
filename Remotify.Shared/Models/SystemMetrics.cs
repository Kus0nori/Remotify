namespace Remotify.Shared.Models;

public class SystemMetrics
{
    public double CpuUsage { get; init; }
    public double RamUsage { get; init; }
    public double RamUsedGb { get; init; }
    public double RamTotalGb { get; init; }
    public long NetworkUploadBps { get; init; }
    public long NetworkDownloadBps { get; init; }
    public long UptimeSeconds { get; init; }
}
