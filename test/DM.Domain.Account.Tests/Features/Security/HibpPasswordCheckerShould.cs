using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Account.Features.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Security;

/// <summary>
/// The range lookup, and the fail-open answer when it cannot be made.
/// </summary>
public class HibpPasswordCheckerShould
{
    // SHA-1 of "password" is 5BAA61E4C9B93F3F0682250B6CF8331B7EE68FD8.
    private const string Password = "password";
    private const string Prefix = "5BAA6";
    private const string Suffix = "1E4C9B93F3F0682250B6CF8331B7EE68FD8";

    private static HibpPasswordChecker Checker(StubHandler handler) =>
        new(new HttpClient(handler), NullLogger<HibpPasswordChecker>.Instance);

    [Fact]
    public async Task FindAPasswordWhoseSuffixTheRangeLists()
    {
        var handler = StubHandler.Answering($"0018A45C4D1DEF81644B54AB7F969B88D65:1\r\n{Suffix}:9545824\r\n");

        var compromised = await Checker(handler).IsCompromisedAsync(Password);

        compromised.Should().BeTrue();
        handler.RequestedUri!.AbsolutePath.Should().EndWith($"/range/{Prefix}");
    }

    [Fact]
    public async Task NotFindAPasswordTheRangeDoesNotList()
    {
        var handler = StubHandler.Answering("0018A45C4D1DEF81644B54AB7F969B88D65:1\r\n");

        (await Checker(handler).IsCompromisedAsync(Password)).Should().BeFalse();
    }

    [Fact]
    public async Task AllowThePasswordWhenTheServiceRefuses()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent($"{Suffix}:1")
        });

        (await Checker(handler).IsCompromisedAsync(Password)).Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task AllowThePasswordWhenTheServiceCannotBeAsked(Type failure)
    {
        var handler = new StubHandler(_ => throw (Exception)Activator.CreateInstance(failure)!);

        (await Checker(handler).IsCompromisedAsync(Password)).Should().BeFalse();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? RequestedUri { get; private set; }

        public static StubHandler Answering(string body) =>
            new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUri = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }
}
