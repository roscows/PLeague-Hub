using System.Net;

namespace PLeagueHub.Api.Services.News;

public interface IPublicAddressResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken = default);
}

public sealed class PublicAddressResolver : IPublicAddressResolver
{
    public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken = default)
    {
        if (IPAddress.TryParse(host, out var literal)) return [literal];
        return await Dns.GetHostAddressesAsync(host, cancellationToken);
    }
}

public static class PublicAddressValidator
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] switch
            {
                0 or 10 or 127 => false,
                100 when bytes[1] is >= 64 and <= 127 => false,
                169 when bytes[1] == 254 => false,
                172 when bytes[1] is >= 16 and <= 31 => false,
                192 when bytes[1] == 0 && bytes[2] is 0 or 2 => false,
                192 when bytes[1] == 88 && bytes[2] == 99 => false,
                192 when bytes[1] == 168 => false,
                198 when bytes[1] is 18 or 19 => false,
                198 when bytes[1] == 51 && bytes[2] == 100 => false,
                203 when bytes[1] == 0 && bytes[2] == 113 => false,
                >= 224 => false,
                _ => true
            };
        }

        if (bytes[0] is 0xfc or 0xfd || (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80) || bytes[0] == 0xff)
            return false;

        return !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8);
    }
}
