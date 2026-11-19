using System.Net;
using OpsPulse.Api.Configuration;
using OpsPulse.Api.Domain;
using OpsPulse.Api.Monitoring;

namespace OpsPulse.Tests;

public sealed class ProbeSafetyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("198.18.0.1")]
    [InlineData("198.51.100.12")]
    [InlineData("203.0.113.5")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:7f00:1::")]
    [InlineData("64:ff9b::7f00:1")]
    public void Rejects_private_and_special_use_addresses(string address)
        => Assert.False(PublicNetwork.IsPublicAddress(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    public void Accepts_public_addresses(string address)
        => Assert.True(PublicNetwork.IsPublicAddress(IPAddress.Parse(address)));

    [Theory]
    [InlineData("http://api.example.com/health")]
    [InlineData("https://api.example.com:8443/health")]
    [InlineData("https://user:secret@api.example.com/health")]
    [InlineData("https://api.example.com/health?token=secret")]
    [InlineData("https://api.example.com/health#fragment")]
    [InlineData("https://unapproved.example/health")]
    [InlineData("https://127.0.0.1/health")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not-a-url")]
    public void Rejects_unsafe_or_unapproved_endpoints(string endpoint)
        => Assert.False(MonitoringOptionsValidator.IsAllowedEndpoint(endpoint, ["api.example.com"]));

    [Fact]
    public void Accepts_explicitly_configured_https_endpoint()
        => Assert.True(MonitoringOptionsValidator.IsAllowedEndpoint("https://api.example.com/health", ["api.example.com"]));

    [Theory]
    [InlineData(200, 30, ServiceStatus.Healthy)]
    [InlineData(204, 1000, ServiceStatus.Degraded)]
    [InlineData(429, 30, ServiceStatus.Degraded)]
    [InlineData(301, 30, ServiceStatus.Down)]
    [InlineData(401, 30, ServiceStatus.Down)]
    [InlineData(503, 30, ServiceStatus.Down)]
    public void Classifies_http_outcomes(int code, int elapsed, ServiceStatus expected)
        => Assert.Equal(expected, SafeHttpProbe.Classify(code, elapsed, 1000));

    [Fact]
    public void Startup_validation_rejects_duplicate_ids_and_invalid_poll_interval()
    {
        var options = new MonitoringOptions
        {
            IntervalSeconds = 0,
            Services = [new() { Id = "payments", Name = "Payments" }, new() { Id = "payments", Name = "Duplicate" }]
        };
        var result = new MonitoringOptionsValidator().Validate(null, options);
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("unique"));
        Assert.Contains(result.Failures, failure => failure.Contains("IntervalSeconds"));
    }
}
