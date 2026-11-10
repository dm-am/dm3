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

// A session token opens to the pair it was minted with, and to nothing else.
public class SessionTokenShould
{
    private static readonly Guid UserId = Guid.Parse("5e55a0de-0000-4000-8000-000000000001");
    private static readonly Guid SessionId = Guid.Parse("5e55a0de-0000-4000-8000-000000000002");

    private static readonly string Payload = $$"""{"userId":"{{UserId}}","sessionId":"{{SessionId}}"}""";

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

    // The cookies already handed out carry this payload.
    [Fact]
    public async Task OpenATokenOfThePayloadAlreadyIssued()
    {
        var token = await cipher.Encrypt(Payload, SealPurpose.SessionToken);

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
        var token = await cipher.Encrypt(payload, SealPurpose.SessionToken);

        (await SessionToken.Read(cipher, token)).Should().BeNull();
    }

    [Fact]
    public async Task OpenToNothingWhenThePairWasSealedForTheSecondFactorChallenge()
    {
        var sealedForTheChallenge = await cipher.Encrypt(Payload, SealPurpose.TwoFactorChallenge);

        (await SessionToken.Read(cipher, sealedForTheChallenge)).Should().BeNull();
    }

    [Fact]
    public async Task OpenToNothingWhenThePairWasSealedForNoPurpose()
    {
        var sealedForNoPurpose = await cipher.Encrypt(Payload);

        (await SessionToken.Read(cipher, sealedForNoPurpose)).Should().BeNull();
    }

    [Fact]
    public async Task NotOpenAsTheSecondFactorChallengeOrAsAStoredValue()
    {
        var token = await new SessionToken(UserId, SessionId).Write(cipher);

        var asTheChallenge = () => cipher.Decrypt(token, SealPurpose.TwoFactorChallenge);
        var asAStoredValue = () => cipher.Decrypt(token);

        await asTheChallenge.Should().ThrowAsync<AuthenticationTagMismatchException>();
        await asAStoredValue.Should().ThrowAsync<AuthenticationTagMismatchException>();
    }

    [Theory]
    [InlineData("not base64 at all")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==")]
    public async Task OpenToNothingWhenThisServerDidNotMintIt(string token)
    {
        (await SessionToken.Read(cipher, token)).Should().BeNull();
    }
}
