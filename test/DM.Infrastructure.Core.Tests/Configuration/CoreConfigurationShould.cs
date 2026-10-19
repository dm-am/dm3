using System;
using System.Collections.Generic;
using DM.Domain.Core.Configuration;
using DM.Infrastructure.Core.Configuration;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Configuration;

/// <summary>
/// A host is refused what it cannot work with, and asked for nothing else.
/// </summary>
/// <remarks>Validation runs through the startup validator, as the host runs it: binding alone never fails.</remarks>
public class CoreConfigurationShould
{
    [Fact]
    public void AskNoStoreOfAHostThatDeclaresNone() =>
        Validate(new Dictionary<string, string?> { ["SiteAddressConfiguration:PublicUrl"] = "http://localhost:5173" })
            .Should().BeNull(
                "a worker that opens neither store must be able to start without naming " +
                "either, or the demand stops meaning anything for the hosts that do");

    [Fact]
    public void RefuseAHostThatDeclaresTheRelationalStoreWithoutOne() =>
        Validate(
                new Dictionary<string, string?> { ["SiteAddressConfiguration:PublicUrl"] = "http://localhost:5173" },
                services => services.RequireRelationalStorage())
            .Should().Contain("ConnectionStrings:Rdb");

    [Fact]
    public void RefuseAHostThatDeclaresObjectStorageWithoutCredentials() =>
        Validate(
                new Dictionary<string, string?>
                {
                    ["SiteAddressConfiguration:PublicUrl"] = "http://localhost:5173",
                    ["CdnConfiguration:Url"] = "http://localhost:9000",
                },
                services => services.RequireObjectStorage())
            .Should().Contain("CdnConfiguration");

    [Fact]
    public void BindTheSectionsTheModuleReads()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CdnConfiguration:PublicUrl"] = "https://cdn.example",
            ["GeoLocationConfiguration:CountryDatabasePath"] = "/data/country.csv",
            ["BotConfiguration:TelegramBotToken"] = "token",
        }).Build();

        using var provider = new ServiceCollection().AddDmCoreConfiguration(configuration).BuildServiceProvider();

        provider.GetRequiredService<IOptions<CdnConfiguration>>().Value.PublicUrl
            .Should().Be("https://cdn.example");
        provider.GetRequiredService<IOptions<GeoLocationConfiguration>>().Value.CountryDatabasePath
            .Should().Be("/data/country.csv");
        provider.GetRequiredService<IOptions<BotConfiguration>>().Value.TelegramBotToken
            .Should().Be("token");
    }

    /// <summary>The message of the first failure, or null when the host would start.</summary>
    private static string? Validate(
        IDictionary<string, string?> settings, Action<IServiceCollection>? declare = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddOptions().AddDmCoreConfiguration(configuration);
        declare?.Invoke(services);

        using var provider = services.BuildServiceProvider();
        try
        {
            // Registered by the first ValidateOnStart: a host that declares nothing has nothing to refuse.
            provider.GetService<IStartupValidator>()?.Validate();
            return null;
        }
        catch (OptionsValidationException exception)
        {
            return exception.Message;
        }
    }
}
