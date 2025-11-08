namespace ZabbixAgent.Services;

public class AgentService
{
    private readonly ILogger<AgentService> _logger;
    private readonly Configurations _configurations;
    private TcpListener _listener;
    private bool _isRunning;
    private readonly string _allowedSubnet;
    private readonly string _version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
    private readonly RadiusChecker _radiusChecker;
    private readonly TcpChecker _tcpChecker;

    public AgentService(ILogger<AgentService> logger, IConfiguration configuration, RadiusChecker radiusChecker, TcpChecker tcpChecker)
    {
        _logger = logger;
        _configurations = configuration.GetSection("Configurations").Get<Configurations>();
        _radiusChecker = radiusChecker;
        _tcpChecker = tcpChecker;
        _allowedSubnet = _configurations.AllowedSubnet;

        if (string.IsNullOrWhiteSpace(_allowedSubnet))
        {
            _logger.LogWarning("Warning: No allowed subnets configured. All connections will be permitted.");
        }
        else
        {
            _logger.LogWarning("Configured allowed subnet: {_allowedSubnet}", _allowedSubnet);
        }
    }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Parse("0.0.0.0"), _configurations.Port);
        _listener.Start();
        _isRunning = true;
        _logger.LogInformation($"Zabbix Agent listening...");

        try
        {
            while (_isRunning)
            {
                _logger.LogInformation("Waiting for a connection...");
                TcpClient client = await _listener.AcceptTcpClientAsync();

                if (client.Client.RemoteEndPoint is IPEndPoint remoteIpEndPoint)
                {
                    IPAddress clientIp = remoteIpEndPoint.Address;
                    if (!IsClientAllowed(clientIp))
                    {
                        _logger.LogError("Rejected connection from unauthorized IP: {clientIp}!", clientIp);
                        client.Close();
                        continue;
                    }
                    _logger.LogInformation("Client connected from authorized IP: {clientIp}.", clientIp);
                }
                else
                {
                    _logger.LogWarning("Client connected, but could not determine remote IP. Proceeding with caution.");
                }
                _ = HandleClientAsync(client);
            }
        }
        catch (SocketException ex)
        {
            _logger.LogError("Socket exception: {ex.Message}", ex.Message);
        }
        finally
        {
            _listener.Stop();
            _logger.LogInformation("Zabbix Agent stopped.");
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();
    }

    private bool IsClientAllowed(IPAddress clientIp)
    {
        if (string.IsNullOrWhiteSpace(_allowedSubnet))
        {
            return true;
        }
        return IPSubnetService.IsInSubnet(clientIp, _allowedSubnet);
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        {
            try
            {
                byte[] headerBuffer = new byte[13];
                int bytesRead = await stream.ReadAsync(headerBuffer);
                if (bytesRead < 5 || Encoding.ASCII.GetString(headerBuffer, 0, 4) != "ZBXD")
                {
                    _logger.LogWarning("Invalid Zabbix protocol header.");
                    return;
                }

                if (headerBuffer[4] != 0x01)
                {
                    _logger.LogWarning("Unsupported Zabbix protocol version: {headerBuffer}", headerBuffer[4]);
                    return;
                }

                byte[] lengthBytes = new byte[8];
                Buffer.BlockCopy(headerBuffer, 5, lengthBytes, 0, 8);
                long dataLength = BitConverter.ToInt64(lengthBytes, 0);

                byte[] dataBuffer = new byte[dataLength];
                int totalBytesRead = 0;
                while (totalBytesRead < dataLength)
                {
                    int currentRead = await stream.ReadAsync(dataBuffer.AsMemory(totalBytesRead, (int)dataLength - totalBytesRead));
                    if (currentRead == 0)
                    {
                        _logger.LogWarning("Client disconnected unexpectedly.");
                        return;
                    }
                    totalBytesRead += currentRead;
                }

                string request = Encoding.UTF8.GetString(dataBuffer, 0, (int)dataLength);
                _logger.LogInformation("Received request: {request}", request);

                string responseString;

                // Try to parse as JSON first
                if (request.Trim().StartsWith('{') && request.Trim().EndsWith('}'))
                {
                    try
                    {
                        var jsonRequest = JsonSerializer.Deserialize<ZabbixPassiveCheckRequest>(request);
                        if (jsonRequest != null && jsonRequest.Request == "passive checks" && jsonRequest.Data != null)
                        {
                            var jsonResponse = new ZabbixPassiveCheckResponse();
                            foreach (var item in jsonRequest.Data)
                            {
                                string value = GetItemValue(item.Key);
                                jsonResponse.Data.Add(new ZabbixPassiveCheckResult { Key = item.Key, Value = value });
                            }
                            responseString = JsonSerializer.Serialize(jsonResponse);
                        }
                        else
                        {
                            // It was JSON, but not the expected "passive checks" format.
                            // In this case, we don't have a specific value, so we'll treat it as unsupported.
                            // A Zabbix server usually won't send other types of JSON passive checks.
                            _logger.LogWarning("Unexpected JSON request format. Responding with ZBX_NOTSUPPORTED.");
                            responseString = "ZBX_NOTSUPPORTED";
                        }
                    }
                    catch (JsonException ex)
                    {
                        // If it looked like JSON but failed to parse, it's likely malformed or not the expected Zabbix JSON.
                        // For a plain zabbix_get, this path won't be hit because it won't start with '{'.
                        _logger.LogWarning("Could not deserialize JSON request: {ex.Message}. Responding with ZBX_NOTSUPPORTED.", ex.Message);
                        responseString = "ZBX_NOTSUPPORTED";
                    }
                }
                else
                {
                    // Not JSON, treat as a plain item key
                    responseString = GetItemValue(request);
                }

                byte[] responseBytes = Encoding.UTF8.GetBytes(responseString);

                // Prepare Zabbix response: ZBXD (4 bytes), 0x01 (1 byte), Length (8 bytes), Data (n bytes)
                byte[] zabbixResponseHeader = new byte[13];
                Encoding.ASCII.GetBytes("ZBXD").CopyTo(zabbixResponseHeader, 0);
                zabbixResponseHeader[4] = 0x01; // Protocol version
                BitConverter.GetBytes((long)responseBytes.Length).CopyTo(zabbixResponseHeader, 5);

                await stream.WriteAsync(zabbixResponseHeader);
                await stream.WriteAsync(responseBytes);
                _logger.LogInformation("Sent response: {responseString}", responseString);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error handling client: {ex.Message}", ex.Message);
            }
        }
    }

    private string GetItemValue(string itemKey)
    {
        switch (itemKey)
        {
            case "system.uptime":
                return ((int)DateTime.Now.Subtract(System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalSeconds).ToString();

            case "agent.ping":
                return "1";

            case "agent.version":
                return _version;

            case "agent.hostname":
                return Environment.GetEnvironmentVariable("COMPUTERNAME")
                       ?? Environment.GetEnvironmentVariable("HOSTNAME");
        }

        // Handle net.tcp.port[host,port]
        if (itemKey.StartsWith("net.tcp.port", StringComparison.OrdinalIgnoreCase))
        {
            return _tcpChecker.IsTcpPortListening(itemKey);
        }

        // Handle net.radius.port[host,port]
        if (itemKey.StartsWith("net.radius.port", StringComparison.OrdinalIgnoreCase))
        {
            return _radiusChecker.IsRadiusListening(itemKey);
        }

        _logger.LogWarning("Unknown item key received: {itemKey}", itemKey);
        return "ZBX_NOTSUPPORTED";
    }
}
