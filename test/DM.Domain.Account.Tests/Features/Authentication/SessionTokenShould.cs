using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Authentication;

/// <summary>
/// A session token opens to the pair it was minted with, and to nothing else.
/// </summary>
public class SessionTokenShould
{
    private static readonly Guid UserId = Guid.Parse("5e55a0de-0000-4000-8000-000000000001");
    private static readonly Guid SessionId = Guid.Parse("5e55a0de-0000-4000-8000-000000000002");

    private readonly AesGcmSymmetricCryptoService cipher = new(Options.Create(new CryptoConfiguration
    {
        KeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        KeyVersion = 1,
        PreviousKeys = new Dictionary<int, string>()
    }));

    [Fact]
    public async Task OpenToThePairItWasMintedWith()
    {
        var token = await new SessionToken(UserId, SessionId).Write(cipher);

        (await SessionToken.Read(cipher, token)).Should().Be(new SessionToken(UserId, SessionId));
    }

    /// <summary>The cookies already handed out carry this payload.</summary>
    [Fact]
    public async Task OpenATokenOfThePayloadAlreadyIssued()
    {
        var token = await cipher.Encrypt($$"""{"userId":"{{UserId}}","sessionId":"{{SessionId}}"}""");

        (await SessionToken.Read(cipher, token)).Should().Be(new SessionToken(UserId, SessionId));
    }

    [Theory]
    [InlineData("""{"userId":"5e55a0de-0000-4000-8000-000000000001"}""")]
    [InlineData("""{"sessionId":"5e55a0de-0000-4000-8000-000000000002"}""")]
    [InlineData("""{"userId":"someone","sessionId":"5e55a0de-0000-4000-8000-000000000002"}""")]
    [InlineData("""not json""")]
    [InlineData("""null""")]
    public async Task OpenToNothingWhenThePayloadDoesNotNameBothHalves(string payload)
    {
        var token = await cipher.Encrypt(payload);

        (await SessionToken.Read(cipher, token)).Should().BeNull();
    }

    [Theory]
    [InlineData("not base64 at all")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==")]
    public async Task OpenToNothingWhenThisServerDidNotMintIt(string token)
    {
        (await SessionToken.Read(cipher, token)).Should().BeNull();
    }
}
