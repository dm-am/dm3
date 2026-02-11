using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DM.Services.Authentication.Configuration;
using DM.Services.Authentication.Implementation.Security;
using DM.Tests.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Services.Authentication.Tests;

public class AesGcmSymmetricCryptoServiceShould : UnitTestBase
{
    private static string GenerateKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static AesGcmSymmetricCryptoService CreateService(CryptoConfiguration config) =>
        new(Options.Create(config), NullLogger<AesGcmSymmetricCryptoService>.Instance);

    [Fact]
    public async Task EncryptAndDecryptRoundTrip()
    {
        var config = new CryptoConfiguration
        {
            KeyBase64 = GenerateKey(),
            KeyVersion = 1
        };
        var service = CreateService(config);

        var plaintext = "hello world";
        var encrypted = await service.Encrypt(plaintext);
        var decrypted = await service.Decrypt(encrypted);

        decrypted.Should().Be(plaintext);
        encrypted.Should().NotBe(plaintext);
    }

    [Fact]
    public async Task ProduceDifferentCiphertextForSameInput()
    {
        var config = new CryptoConfiguration
        {
            KeyBase64 = GenerateKey(),
            KeyVersion = 1
        };
        var service = CreateService(config);

        var plaintext = "same value";
        var encrypted1 = await service.Encrypt(plaintext);
        var encrypted2 = await service.Encrypt(plaintext);

        encrypted1.Should().NotBe(encrypted2, "random nonce should make each encryption unique");

        var decrypted1 = await service.Decrypt(encrypted1);
        var decrypted2 = await service.Decrypt(encrypted2);

        decrypted1.Should().Be(plaintext);
        decrypted2.Should().Be(plaintext);
    }

    [Fact]
    public async Task DecryptWithCurrentKeyVersion()
    {
        var config = new CryptoConfiguration
        {
            KeyBase64 = GenerateKey(),
            KeyVersion = 1
        };
        var service = CreateService(config);

        var plaintext = "test message";
        var encrypted = await service.Encrypt(plaintext);

        // Decode to verify version byte
        var encryptedBytes = Convert.FromBase64String(encrypted);
        var version = encryptedBytes[0];

        version.Should().Be(1, "should use key version 1");

        var decrypted = await service.Decrypt(encrypted);
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public async Task DecryptWithPreviousKeyAfterRotation()
    {
        var keyV1 = GenerateKey();
        var keyV2 = GenerateKey();

        // Create service with v1 key
        var configV1 = new CryptoConfiguration
        {
            KeyBase64 = keyV1,
            KeyVersion = 1
        };
        var serviceV1 = CreateService(configV1);

        // Encrypt with v1
        var plaintext = "encrypted with old key";
        var encrypted = await serviceV1.Encrypt(plaintext);

        // Create new service with v2 key and v1 in PreviousKeys
        var configV2 = new CryptoConfiguration
        {
            KeyBase64 = keyV2,
            KeyVersion = 2,
            PreviousKeys = new Dictionary<int, string>
            {
                { 1, keyV1 }
            }
        };
        var serviceV2 = CreateService(configV2);

        // Should be able to decrypt with v2 service (using v1 from PreviousKeys)
        var decrypted = await serviceV2.Decrypt(encrypted);
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void ThrowOnInvalidKey()
    {
        var config = new CryptoConfiguration
        {
            KeyBase64 = "",
            KeyVersion = 1
        };

        var act = () => CreateService(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*KeyBase64 must be configured*");
    }

    [Fact]
    public void ThrowOnWrongKeyLength()
    {
        // Generate a 16-byte key (128 bits) instead of 32 bytes (256 bits)
        var invalidKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var config = new CryptoConfiguration
        {
            KeyBase64 = invalidKey,
            KeyVersion = 1
        };

        var act = () => CreateService(config);

        act.Should().Throw<CryptographicException>()
            .WithMessage("*32 bytes*");
    }

    [Fact]
    public async Task ThrowOnTamperedCiphertext()
    {
        var config = new CryptoConfiguration
        {
            KeyBase64 = GenerateKey(),
            KeyVersion = 1
        };
        var service = CreateService(config);

        var plaintext = "secure message";
        var encrypted = await service.Encrypt(plaintext);

        // Tamper with the ciphertext by modifying one character
        var tamperedBytes = Convert.FromBase64String(encrypted);
        tamperedBytes[tamperedBytes.Length - 1] ^= 0xFF; // Flip bits in last byte
        var tampered = Convert.ToBase64String(tamperedBytes);

        var act = async () => await service.Decrypt(tampered);

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task EncryptUnicodeText()
    {
        var config = new CryptoConfiguration
        {
            KeyBase64 = GenerateKey(),
            KeyVersion = 1
        };
        var service = CreateService(config);

        var plaintext = "Привет мир";
        var encrypted = await service.Encrypt(plaintext);
        var decrypted = await service.Decrypt(encrypted);

        decrypted.Should().Be(plaintext, "Cyrillic text should round-trip correctly");
    }
}
