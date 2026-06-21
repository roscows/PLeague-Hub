using System.Net;
using System.Net.Sockets;

namespace PLeagueHub.Api.Services.News;

public sealed class SafeNewsSocketConnector
{
    private readonly IPublicAddressResolver _resolver;

    public SafeNewsSocketConnector(IPublicAddressResolver resolver)
    {
        _resolver = resolver;
    }

    public async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await _resolver.ResolveAsync(context.DnsEndPoint.Host, cancellationToken);
        var publicAddresses = addresses.Where(PublicAddressValidator.IsPublic).ToArray();
        if (publicAddresses.Length == 0)
            throw new HttpRequestException("Adresa izvora nije javna.");

        Exception? lastError = null;
        foreach (var address in publicAddresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                lastError = exception;
                socket.Dispose();
                if (exception is OperationCanceledException) throw;
            }
        }

        throw new HttpRequestException("Povezivanje sa izvorom nije uspelo.", lastError);
    }
}
