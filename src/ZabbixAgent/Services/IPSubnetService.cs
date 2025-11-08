namespace ZabbixAgent.Services;

public static class IPSubnetService
{
    public static bool IsInSubnet(IPAddress ipAddress, string subnetCidr)
    {
        var parts = subnetCidr.Split('/');
        if (parts.Length != 2)
            throw new FormatException("Invalid CIDR notation. Example: 192.168.1.0/24");

        var baseAddress = IPAddress.Parse(parts[0]);
        var prefixLength = int.Parse(parts[1]);

        if (baseAddress.AddressFamily != ipAddress.AddressFamily)
            return false;

        var baseBytes = baseAddress.GetAddressBytes();
        var addressBytes = ipAddress.GetAddressBytes();

        var baseInt = new BigInteger(baseBytes, isUnsigned: true, isBigEndian: true);
        var addrInt = new BigInteger(addressBytes, isUnsigned: true, isBigEndian: true);

        int totalBits = baseBytes.Length * 8;
        BigInteger mask = GetSubnetMask(prefixLength, totalBits);

        return (baseInt & mask) == (addrInt & mask);
    }

    private static BigInteger GetSubnetMask(int prefixLength, int totalBits)
    {
        if (prefixLength < 0 || prefixLength > totalBits)
            throw new ArgumentOutOfRangeException(nameof(prefixLength), "Invalid prefix length.");

        BigInteger mask = BigInteger.Zero;
        for (int i = 0; i < prefixLength; i++)
        {
            mask |= (BigInteger.One << (totalBits - 1 - i));
        }

        return mask;
    }
}
