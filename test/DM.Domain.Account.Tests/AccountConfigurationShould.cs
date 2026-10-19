using System;
using System.Collections.Generic;
using AwesomeAssertions;
using DM.Domain.Account.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Domain.Account.Tests;

/// <summary>
/// The encryption key is checked where the host starts, the only place it is checked.
/// </summary>
public class AccountConfigurationShould
{
    private static CryptoConfiguration CryptoWith(string? key)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CryptoConfiguration:KeyBase64"] = key,
            })
            .Build();

        using var provider = new ServiceCollection()
            .AddDmAccountConfiguration(configuration)
            .BuildServiceProvider();

        return provider.GetRequiredService<IOptions<CryptoConfiguration>>().Value;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")]
    public void RefuseAKeyThatIsNotThirtyTwoBytes(string? key)
    {
        var read = () => CryptoWith(key);

        read.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AcceptAKeyOfThirtyTwoBytes()
    {
        var key = Convert.ToBase64String(new byte[32]);

        CryptoWith(key).KeyBase64.Should().Be(key);
    }
}
