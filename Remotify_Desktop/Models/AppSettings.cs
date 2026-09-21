namespace Remotify.Models;

public class AppSettings
{
    public int Port { get; set; } = 5123;
    public string AuthToken { get; set; } = string.Empty;
    public bool AutoStart { get; set; }
    public bool FirewallRuleEnabled { get; set; }
    public bool FirstRunCompleted { get; set; }
}
