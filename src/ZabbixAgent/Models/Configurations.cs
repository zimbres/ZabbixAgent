namespace ZabbixAgent.Models;

internal class Configurations
{
    public int Port { get; set; } = 10050;
    public string AllowedSubnet { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
}
