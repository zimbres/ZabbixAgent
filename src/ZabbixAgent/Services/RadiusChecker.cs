namespace ZabbixAgent.Services;

public class RadiusChecker
{
    private readonly ILogger<RadiusChecker> _logger;

    public RadiusChecker(ILogger<RadiusChecker> logger)
    {
        _logger = logger;
    }

    public string IsRadiusListening(string itemKey)
    {
        string inner = itemKey["net.radius.port[".Length..].TrimEnd(']');
        string[] parts = inner.Split(',');
        var host = parts[0];
        if (parts.Length != 2 || !int.TryParse(parts[1], out int port))
            return "ZBX_NOTSUPPORTED";
        try
        {
            byte[] request = BuildAccessRequest("sharedSecret");
            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = 2000;
            udp.Send(request, request.Length, host, port);

            var remoteEP = new IPEndPoint(IPAddress.Any, 0);
            var asyncResult = udp.BeginReceive(null, null);
            if (asyncResult.AsyncWaitHandle.WaitOne(2000))
            {
                byte[] response = udp.EndReceive(asyncResult, ref remoteEP);
                if (response != null && response.Length > 20 && response[0] >= 2 && response[0] <= 5)
                {
                    // Valid RADIUS codes are 1–5 (Access-Request, Accept, Reject, Challenge, Accounting)
                    return "1";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking RADIUS server at {host}:{port}.", host, port);
        }

        return "0";
    }

    private static byte[] BuildAccessRequest(string secret)
    {
        // RADIUS Access-Request (Code 1)
        byte[] packet = new byte[20]; // Minimum RADIUS header length
        packet[0] = 1; // Code = Access-Request
        packet[1] = 1; // Identifier

        ushort length = (ushort)packet.Length;
        packet[2] = (byte)(length >> 8);
        packet[3] = (byte)(length & 0xFF);

        // Random 16-byte authenticator
        byte[] authenticator = new byte[16];
        RandomNumberGenerator.Fill(authenticator);
        Array.Copy(authenticator, 0, packet, 4, 16);

        // (Optional) you could add attributes like User-Name or NAS-Identifier, but not needed just to test listening
        return packet;
    }
}
