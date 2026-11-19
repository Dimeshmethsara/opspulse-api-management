using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using OpsPulse.Api.Configuration;
using OpsPulse.Api.Domain;

namespace OpsPulse.Api.Monitoring;

public sealed record ProbeResult(ServiceStatus Status, int? ResponseTimeMs);
public interface IHealthProbe
{
    Task<ProbeResult> CheckAsync(ServiceDefinition service, CancellationToken cancellationToken);
}

public sealed class SafeHttpProbe(IOptions<MonitoringOptions> options, IHttpClientFactory clients) : IHealthProbe
{
    public async Task<ProbeResult> CheckAsync(ServiceDefinition service, CancellationToken cancellationToken)
    {
        // The worker supplies this server-owned definition. There is no URL input in any API contract.
        if (service.Kind == ProbeKind.Demo)
            return new(service.DemoStatus, service.DemoStatus == ServiceStatus.Down ? null : service.DemoLatencyMs);
        if (!MonitoringOptionsValidator.IsAllowedEndpoint(service.Endpoint, options.Value.AllowedHosts))
            return new(ServiceStatus.Down, null);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        var elapsed = Stopwatch.StartNew();
        try
        {
            using var client = clients.CreateClient("SafeProbe");
            using var request = new HttpRequestMessage(HttpMethod.Get, service.Endpoint);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var milliseconds = (int)Math.Min(elapsed.ElapsedMilliseconds, int.MaxValue);
            return new(Classify((int)response.StatusCode, milliseconds, options.Value.DegradedAfterMs), milliseconds);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new(ServiceStatus.Down, null);
        }
    }

    public static ServiceStatus Classify(int statusCode, int elapsedMs, int degradedAfterMs) => statusCode switch
    {
        >= 200 and < 300 => elapsedMs >= degradedAfterMs ? ServiceStatus.Degraded : ServiceStatus.Healthy,
        429 => ServiceStatus.Degraded,
        _ => ServiceStatus.Down
    };
}

public static class PublicNetwork
{
    // Resolve at connection time and connect to that exact validated address to prevent DNS rebinding.
    // Proxies and redirects are disabled on the handler registered in Program.cs.
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            throw new HttpRequestException("The probe destination is not a public network address.");
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException) { socket.Dispose(); }
            catch { socket.Dispose(); throw; }
        }
        throw new HttpRequestException("The probe destination could not be reached.");
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var a = bytes[0]; var b = bytes[1]; var c = bytes[2];
            return !(a is 0 or 10 or 127 || a >= 224 ||
                a == 100 && b is >= 64 and <= 127 ||
                a == 169 && b == 254 || a == 172 && b is >= 16 and <= 31 ||
                a == 192 && (b == 168 || b == 0 || b == 2 || b == 88 && c == 99) ||
                a == 198 && (b is 18 or 19 || b == 51 && c == 100) ||
                a == 203 && b == 0 && c == 113);
        }
        // Accept global unicast only, excluding special-use and documentation prefixes.
        return address.AddressFamily == AddressFamily.InterNetworkV6 &&
            (bytes[0] & 0xe0) == 0x20 &&
            !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] <= 0x01) &&
            !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8) &&
            !(bytes[0] == 0x20 && bytes[1] == 0x02) &&
            !(bytes[0] == 0x3f && bytes[1] == 0xff && (bytes[2] & 0xf0) == 0);
    }
}
