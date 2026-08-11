using System;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Security;
using DM.Testing;
using DM.Web.API.Middleware;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// The partition key decides whose budget a request spends.
/// </summary>
/// <remarks>
/// Asserted on the key rather than by counting to 429: the limiter itself is
/// framework code, and the key is the whole of what this project decides. The
/// middleware is run for real because it is the half that supplies the account —
/// a partitioner handed a bare context can only answer "address", which is
/// exactly the failure being kept out.
/// </remarks>
public class RateLimitPartitioningShould : UnitTestBase
{
    private const string Address = "203.0.113.9";
    private const string OtherAddress = "198.51.100.4";

    private static readonly ApiCredentialsStorage CredentialsStorage =
        new(Options.Create(new AuthenticationConfiguration()));

    private readonly Mock<ISymmetricCryptoService> cryptoService;

    public RateLimitPartitioningShould()
    {
        cryptoService = Mock<ISymmetricCryptoService>();
    }

    [Fact]
    public async Task CountTwoAccountsBehindOneAddressApart()
    {
        var first = await PartitionKey(Address, TokenOf(Guid.NewGuid()));
        var second = await PartitionKey(Address, TokenOf(Guid.NewGuid()));

        first.Should().NotBe(second,
            "an office, a campus and a mobile carrier are one address, and everyone " +
            "behind it would otherwise spend one budget");
    }

    [Fact]
    public async Task CountOneAccountAsOneCallerWhateverAddressItComesFrom()
    {
        var token = TokenOf(Guid.NewGuid());

        var here = await PartitionKey(Address, token);
        var there = await PartitionKey(OtherAddress, token);

        here.Should().Be(there,
            "an account that changes address would otherwise be handed a second budget");
    }

    [Fact]
    public async Task CountGuestsBehindOneAddressTogether()
    {
        var first = await PartitionKey(Address, cookie: null);
        var second = await PartitionKey(Address, cookie: null);
        var elsewhere = await PartitionKey(OtherAddress, cookie: null);

        first.Should().Be(second, "a guest is known by nothing but the address it arrives from");
        first.Should().NotBe(elsewhere, "two addresses are two callers");
    }

    [Fact]
    public async Task CountATokenThisServerDidNotMintAsAGuest()
    {
        cryptoService.Setup(c => c.Decrypt("forged")).ThrowsAsync(new CryptographicException());

        var forged = await PartitionKey(Address, "forged");

        forged.Should().Be(await PartitionKey(Address, cookie: null),
            "a cookie nobody minted must buy no budget of its own — otherwise the policy " +
            "is bypassed by sending a different random string every request");
    }

    [Fact]
    public async Task CountTheGlobalBudgetByAddressEvenForAnAccount()
    {
        var token = TokenOf(Guid.NewGuid());

        var authenticated = await PartitionKey(Address, token, RateLimitingExtensions.Partition.Address);
        var guest = await PartitionKey(Address, cookie: null, RateLimitingExtensions.Partition.Address);

        authenticated.Should().Be(guest,
            "the global budget is what the host can serve at all, and it is served over addresses");
    }

    [Fact]
    public async Task CountADualStackPeerUnderOneAddress()
    {
        var mapped = await PartitionKey($"::ffff:{Address}", cookie: null);
        var plain = await PartitionKey(Address, cookie: null);

        mapped.Should().Be(plain,
            "the same client arrives in two spellings and must not be given two budgets");
    }

    /// <summary>
    /// A token the cipher accepts, carrying the given account. The session half
    /// is deliberately different every time: the budget belongs to the account,
    /// not to the browser it was last opened in.
    /// </summary>
    private string TokenOf(Guid userId)
    {
        var token = $"token-of-{userId}";
        cryptoService
            .Setup(c => c.Decrypt(token))
            .ReturnsAsync($"{{\"userId\":\"{userId}\",\"sessionId\":\"{Guid.NewGuid()}\"}}");
        return token;
    }

    private async Task<string> PartitionKey(string address, string? cookie,
        RateLimitingExtensions.Partition partition = RateLimitingExtensions.Partition.AccountThenAddress)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        if (cookie != null)
        {
            context.Request.Headers["Cookie"] = $"{ApiCredentialsStorage.AuthCookieName}={cookie}";
        }

        var middleware = new RateLimitAccountMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, CredentialsStorage, cryptoService.Object);

        return RateLimitingExtensions.PartitionKey(context, partition);
    }
}
