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
}
