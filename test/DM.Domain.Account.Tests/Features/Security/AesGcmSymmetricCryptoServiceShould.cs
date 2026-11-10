using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Security;

public class AesGcmSymmetricCryptoServiceShould
{
    private const string Secret = "session token";

    private static readonly string FirstKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string SecondKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static AesGcmSymmetricCryptoService Service(
        string key, int version, Dictionary<int, string>? previousKeys = null) =>
        new(Options.Create(new CryptoConfiguration
        {
            KeyBase64 = key,
            KeyVersion = version,
            PreviousKeys = previousKeys ?? new Dictionary<int, string>()
        }));

    [Fact]
    public async Task ReadBackWhatItWrote()
    {
        var service = Service(FirstKey, 1);

        var encrypted = await service.Encrypt(Secret);

        (await service.Decrypt(encrypted)).Should().Be(Secret);
    }

    [Fact]
    public async Task ReadWhatAPreviousKeyWrote()
    {
        var encrypted = await Service(FirstKey, 1).Encrypt(Secret);
        var rotated = Service(SecondKey, 2, new Dictionary<int, string> { [1] = FirstKey });

        (await rotated.Decrypt(encrypted)).Should().Be(Secret);
    }

    [Fact]
    public async Task RefuseAPayloadOfAKeyVersionItDoesNotHold()
    {
        var encrypted = await Service(FirstKey, 1).Encrypt(Secret);
        var rotated = Service(SecondKey, 2);

        var decrypt = () => rotated.Decrypt(encrypted);

        // Refused by the version byte, before any key is tried.
        await decrypt.Should().ThrowExactlyAsync<CryptographicException>();
    }

    [Fact]
    public async Task RefuseATamperedPayload()
    {
        var service = Service(FirstKey, 1);
        var payload = Convert.FromBase64String(await service.Encrypt(Secret));
        payload[^1] ^= 0x01;

        var decrypt = () => service.Decrypt(Convert.ToBase64String(payload));

        await decrypt.Should().ThrowAsync<AuthenticationTagMismatchException>();
    }

    [Theory]
    [InlineData(SealPurpose.SessionToken)]
    [InlineData(SealPurpose.TwoFactorChallenge)]
    public async Task ReadBackWhatItSealedForThePurpose(SealPurpose purpose)
    {
        var service = Service(FirstKey, 1);

        var sealedValue = await service.Encrypt(Secret, purpose);

        (await service.Decrypt(sealedValue, purpose)).Should().Be(Secret);
    }

    // Null stands for the value of the server's own store, sealed for no purpose.
    [Theory]
    [InlineData(SealPurpose.SessionToken, SealPurpose.TwoFactorChallenge)]
    [InlineData(SealPurpose.TwoFactorChallenge, SealPurpose.SessionToken)]
    [InlineData(SealPurpose.SessionToken, null)]
    [InlineData(SealPurpose.TwoFactorChallenge, null)]
    [InlineData(null, SealPurpose.SessionToken)]
    [InlineData(null, SealPurpose.TwoFactorChallenge)]
    public async Task RefuseAValueSealedForAnotherPurpose(SealPurpose? sealedFor, SealPurpose? openedAs)
    {
        var service = Service(FirstKey, 1);
        var sealedValue = sealedFor is { } written
            ? await service.Encrypt(Secret, written)
            : await service.Encrypt(Secret);

        var open = () => openedAs is { } read ? service.Decrypt(sealedValue, read) : service.Decrypt(sealedValue);

        await open.Should().ThrowAsync<AuthenticationTagMismatchException>();
    }

    [Fact]
    public async Task KeepThePurposeAcrossAKeyRotation()
    {
        var sealedValue = await Service(FirstKey, 1).Encrypt(Secret, SealPurpose.SessionToken);
        var rotated = Service(SecondKey, 2, new Dictionary<int, string> { [1] = FirstKey });

        (await rotated.Decrypt(sealedValue, SealPurpose.SessionToken)).Should().Be(Secret);

        var asAnother = () => rotated.Decrypt(sealedValue, SealPurpose.TwoFactorChallenge);
        await asAnother.Should().ThrowAsync<AuthenticationTagMismatchException>();
    }

    [Fact]
    public async Task RefuseAPurposeNobodyNamed()
    {
        var service = Service(FirstKey, 1);
        var sealedForNoPurpose = await service.Encrypt(Secret);

        var seal = () => service.Encrypt(Secret, default);
        var open = () => service.Decrypt(sealedForNoPurpose, default);

        await seal.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await open.Should().ThrowAsync<ArgumentOutOfRangeException>(
            "an unset purpose must not open what was sealed for none");
    }
}
