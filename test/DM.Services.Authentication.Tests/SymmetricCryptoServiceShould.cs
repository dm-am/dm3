using System.Threading.Tasks;
using DM.Services.Authentication.Configuration;
using DM.Services.Authentication.Implementation.Security;
using DM.Tests.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Services.Authentication.Tests;

public class SymmetricCryptoServiceShould : UnitTestBase
{
    private readonly AesGcmSymmetricCryptoService service;

    public SymmetricCryptoServiceShould()
    {
        // Test key: 32 bytes for AES-256
        // "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdef" = 32 bytes -> Base64
        var cryptoConfig = new CryptoConfiguration
        {
            KeyBase64 = "QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVphYmNkZWY=" // 32 bytes
        };
        var cryptoOptions = Options.Create(cryptoConfig);
        service = new AesGcmSymmetricCryptoService(cryptoOptions, NullLogger<AesGcmSymmetricCryptoService>.Instance);
    }

    [Fact]
    public async Task CryptSymmetrically()
    {
        var input = "some value to encrypt";
        var encrypted = await service.Encrypt(input);
        var decrypted = await service.Decrypt(encrypted);

        decrypted.Should().Be(input);
        encrypted.Should().NotBe(input);
    }

    [Fact]
    public async Task ProduceDifferentCiphertextForSameInput()
    {
        // AES-GCM uses random nonce, so same plaintext should produce different ciphertext
        var input = "same value";
        var encrypted1 = await service.Encrypt(input);
        var encrypted2 = await service.Encrypt(input);

        encrypted1.Should().NotBe(encrypted2);

        // But both should decrypt to the same value
        var decrypted1 = await service.Decrypt(encrypted1);
        var decrypted2 = await service.Decrypt(encrypted2);

        decrypted1.Should().Be(input);
        decrypted2.Should().Be(input);
    }
}