using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Infrastructure.Core.Extensions;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Web.API.Tests.Shared.Authentication;

/// <summary>
/// A challenge cookie this server did not write reads as no challenge, whatever is wrong with it.
/// </summary>
/// <remarks>
/// The cipher is the real one, and each row names what the cipher raises on it: the reader catches by type.
/// </remarks>
public class TwoFactorChallengeCookieShould
{
    private const int KeyVersion = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const SealPurpose Purpose = SealPurpose.TwoFactorChallenge;

    private static readonly string Key = NewKey();

    private static readonly ISymmetricCryptoService Cipher = CipherOf(Key, KeyVersion);

    private static readonly TwoFactorChallengeCookie Cookie =
        new(Cipher, Options.Create(new SessionCookieConfiguration()));

    // The forgery, what the cipher raises on it (null when it decrypts), and the cookie itself.
    public static TheoryData<string, Type?, Func<Task<string>>> CookiesNobodyIssued => new()
    {
        { "not base64", typeof(FormatException), () => Task.FromResult("not base64 at all!") },
        {
            "shorter than the envelope", typeof(CryptographicException),
            () => Task.FromResult(Convert.ToBase64String(new byte[10]))
        },
        {
            "an envelope around nothing", typeof(AuthenticationTagMismatchException),
            () => Task.FromResult(Convert.ToBase64String(EnvelopeOfZeros()))
        },
        {
            "tampered ciphertext", typeof(AuthenticationTagMismatchException),
            () => Tampered(bytes => bytes[^1] ^= 0x01)
        },
        {
            "tampered tag", typeof(AuthenticationTagMismatchException),
            () => Tampered(bytes => bytes[1 + NonceSize] ^= 0x01)
        },
        {
            "tampered nonce", typeof(AuthenticationTagMismatchException),
            () => Tampered(bytes => bytes[1] ^= 0x01)
        },
        {
            "cut short", typeof(AuthenticationTagMismatchException),
            async () => Convert.ToBase64String(Convert.FromBase64String(await Issued())[..^4])
        },
        {
            "another key of the same version", typeof(AuthenticationTagMismatchException),
            () => CipherOf(NewKey(), KeyVersion).Encrypt(Guid.NewGuid().ToString(), Purpose)
        },
        {
            "a key version this server does not hold", typeof(CryptographicException),
            () => CipherOf(Key, KeyVersion + 1).Encrypt(Guid.NewGuid().ToString(), Purpose)
        },
        {
            "a session token in place of the challenge", typeof(AuthenticationTagMismatchException),
            () => new SessionToken(Guid.NewGuid(), Guid.NewGuid()).Write(Cipher)
        },
        {
            "a challenge sealed as a session token", typeof(AuthenticationTagMismatchException),
            () => Cipher.Encrypt(Guid.NewGuid().ToString(), SealPurpose.SessionToken)
        },
        {
            "a challenge sealed for no purpose", typeof(AuthenticationTagMismatchException),
            () => Cipher.Encrypt(Guid.NewGuid().ToString())
        },
        { "text that is not an identifier", null, () => Cipher.Encrypt("not a challenge", Purpose) },
        { "nothing encrypted", null, () => Cipher.Encrypt(string.Empty, Purpose) },
    };

    [Theory]
    [MemberData(nameof(CookiesNobodyIssued))]
    public async Task ReadNoChallengeFromACookieNobodyIssued(
        string forgery, Type? raisedByTheCipher, Func<Task<string>> cookie)
    {
        var value = await cookie();

        var read = await Cookie.Read(RequestCarrying(value));

        read.Should().BeNull($"{forgery} is answered as a wrong code is, never by a 500");
        var raised = await Record.ExceptionAsync(() => Cipher.Decrypt(value, Purpose));
        (raised?.GetType())
            .Should().Be(raisedByTheCipher, $"{forgery} has to reach the refusal the row stands for");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ReadNoChallengeFromARequestWithoutOne(string? cookie)
    {
        var read = await Cookie.Read(cookie == null ? new DefaultHttpContext() : RequestCarrying(cookie));

        read.Should().BeNull();
    }

    [Fact]
    public async Task ReadBackTheChallengeTheServerIssued()
    {
        var challengeId = Guid.NewGuid();

        var read = await Cookie.Read(RequestCarrying(await Cipher.Encrypt(challengeId.ToString(), Purpose)));

        read.Should().Be(challengeId);
    }

    [Fact]
    public async Task WriteAChallengeThatOpensOnlyAsAChallenge()
    {
        var challengeId = Guid.NewGuid();
        var answer = new DefaultHttpContext();

        await Cookie.Write(answer, challengeId, TimeSpan.FromMinutes(5));
        var written = WrittenCookie(answer);

        (await Cookie.Read(RequestCarrying(written))).Should().Be(challengeId);
        (await SessionToken.Read(Cipher, written)).Should().BeNull();
        (await Record.ExceptionAsync(() => Cipher.Decrypt(written, SealPurpose.SessionToken)))
            .Should().BeOfType<AuthenticationTagMismatchException>(
                "the purpose has to refuse it, not the shape of what is inside");
        (await Record.ExceptionAsync(() => Cipher.Decrypt(written)))
            .Should().BeOfType<AuthenticationTagMismatchException>();
    }

    private static string WrittenCookie(DefaultHttpContext answer)
    {
        var header = answer.Response.Headers.SetCookie.ToString();
        return Uri.UnescapeDataString(
            header[(TwoFactorChallengeCookie.CookieName.Length + 1)..header.IndexOf(';', StringComparison.Ordinal)]);
    }

    private static Task<string> Issued() => Cipher.Encrypt(Guid.NewGuid().ToString(), Purpose);

    private static async Task<string> Tampered(Action<byte[]> tamper)
    {
        var bytes = Convert.FromBase64String(await Issued());
        tamper(bytes);
        return Convert.ToBase64String(bytes);
    }

    // The version this server holds, then a nonce and a tag of zeros, and no ciphertext.
    private static byte[] EnvelopeOfZeros()
    {
        var envelope = new byte[1 + NonceSize + TagSize];
        envelope[0] = KeyVersion;
        return envelope;
    }

    private static DefaultHttpContext RequestCarrying(string cookie)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie =
            $"{TwoFactorChallengeCookie.CookieName}={Uri.EscapeDataString(cookie)}";
        return context;
    }

    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    // The cipher as the host resolves it: the implementation is internal to the account domain.
    private static ISymmetricCryptoService CipherOf(string key, int version) =>
        new ServiceCollection()
            .AddSingleton(Options.Create(new CryptoConfiguration
            {
                KeyBase64 = key,
                KeyVersion = version,
                PreviousKeys = new Dictionary<int, string>()
            }))
            .AddDefaultTypes(typeof(ISymmetricCryptoService).Assembly)
            .BuildServiceProvider()
            .GetRequiredService<ISymmetricCryptoService>();
}
