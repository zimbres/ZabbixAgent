namespace ZabbixAgent.Services;

public class TcpChecker
{
    private readonly ILogger<TcpChecker> _logger;

    public TcpChecker(ILogger<TcpChecker> logger)
    {
        _logger = logger;
    }

    public string IsTcpPortListening(string itemKey)
    {
        try
        {
            string inner = itemKey["net.tcp.port[".Length..].TrimEnd(']');
            string[] parts = inner.Split(',');

            if (parts.Length != 2) return "ZBX_NOTSUPPORTED";
            string host = parts[0];
            if (!int.TryParse(parts[1], out int port)) return "ZBX_NOTSUPPORTED";

            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (!connectTask.Wait(2000))
                return "0"; // Timeout

            return client.Connected ? "1" : "0";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking TCP port: {itemKey}", itemKey);
            return "0";
        }
    }
}
