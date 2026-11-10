using System;
using DM.Infrastructure.Core.Configuration;
using DM.Web.API.Features.Personal.Webhooks;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

// Through the startup validator, as the host runs it: a key the verifier would throw on must stop the start.
public class DiscordPublicKeyValidationShould
{
    private static readonly string RealKey =
        Convert.ToHexString(new Ed25519PrivateKeyParameters(new SecureRandom()).GeneratePublicKey().GetEncoded());

    [Fact]
    public void LetAHostStartWithTheKeyOfAnApplication() =>
        Validate(RealKey).Should().BeNull();

    [Fact]
    public void AcceptTheKeyInTheLowercaseThePortalShows() =>
        Validate(RealKey.ToLowerInvariant()).Should().BeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LetAHostStartWithNoKeyWhichKeepsTheWebhookClosed(string? key) =>
        Validate(key).Should().BeNull("an unset key is the shipped default");

    [Theory]
    // The right length and alphabet, and the verifier throws on each: the neutral point and two that are no point.
    [InlineData("0100000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("0200000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    // A point of the curve that no key pair has: outside the subgroup every generated key lies in.
    [InlineData("0300000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("not-a-key")]
    [InlineData("abcdef")]
    [InlineData(" ")]
    public void RefuseAKeyTheSignatureCheckCannotUse(string key) =>
        Validate(key).Should().Contain("BotConfiguration:DiscordPublicKey");

    [Fact]
    public void RefuseAKeyWithAStrayCharacterAroundIt() =>
        Validate(RealKey + "\n").Should().Contain("BotConfiguration:DiscordPublicKey",
            "the verifier reads the value as it is, and a pasted line break is not hex");

    private static string? Validate(string? key)
    {
        var services = new ServiceCollection();
        services.AddOptions<BotConfiguration>().Configure(bot => bot.DiscordPublicKey = key);
        services.ValidateDiscordPublicKey();

        using var provider = services.BuildServiceProvider();
        try
        {
            provider.GetRequiredService<IStartupValidator>().Validate();
            return null;
        }
        catch (OptionsValidationException exception)
        {
            return exception.Message;
        }
    }
}
