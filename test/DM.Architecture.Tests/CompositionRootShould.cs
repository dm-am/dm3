using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DM.Domain.Core.Configuration;
using AwesomeAssertions;
using DM.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Built with ValidateScopes and ValidateOnBuild, the options every Program.cs passes. The seeder
/// composes in a private method of its entry point and has a rule of its own.
/// </remarks>
public class CompositionRootShould
{
    private static readonly IReadOnlyList<(string Host, IServiceCollection Services, ServiceProvider Provider)>
        Hosts = BuildHosts();

    [Fact]
    public void BuildTheContainerOfEveryHostItCovers()
    {
        Hosts.Should().NotBeEmpty();
        foreach (var (host, services, _) in Hosts)
        {
            services.Should().NotBeEmpty(
                $"{host} registers a whole application, and an empty graph means the " +
                "rules below check nothing");
        }
    }

    [Fact]
    public void KeepEveryProcessWideComponentFreeOfScopedDependencies()
    {
        foreach (var (host, services, _) in Hosts)
        {
            CaptiveDependencies(services).Should().BeEmpty(
                $"in {host} a component activated once in the root scope keeps whatever it " +
                "was given for the life of the process, so a scoped dependency it takes is " +
                "never released and never refreshed - down to the pooled DbContext behind " +
                "authentication");
        }
    }

    [Fact]
    public void RegisterEveryConfigurationContractItsOwnComponentsAskFor()
    {
        foreach (var (host, services, _) in Hosts)
        {
            UnboundConfigurationContracts(services).Should().BeEmpty(
                $"{host} registers the components that ask for these, and a contract with no " +
                "registration is not a build error: it surfaces as a resolution failure on " +
                "the first request or the first message that reaches the type");
        }
    }

    [Fact]
    public void BindTheOptionsOfEveryAssemblyTheSeederScans()
    {
        var program = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root, "src", "DM.Tools.Seeder", "Program.cs"));

        program.Should().Contain("typeof(ISecurityManager).Assembly",
            "the assertion below is about the assembly this marker names, so a seeder that " +
            "scans another one has to be seen rather than silently excused");

        program.Should().Contain("AddDmAccountConfiguration",
            "the tool scans the whole account assembly, whose types read four option " +
            "sections; IOptions of an unbound type hands out a default rather than " +
            "throwing, so the encryption key would be empty at the first call that " +
            "needs it instead of missing at startup");
    }

    /// <remarks>
    /// The popularity processors it scores with are internal to their modules and found by assembly
    /// scans; the seeder passes ValidateOnBuild.
    /// </remarks>
    [Fact]
    public void SupplyEveryDependencyOfTheSeederOutOfTheContainerTheToolRunsOn()
    {
        using var host = DM.Tools.Seeder.Program.CreateHostBuilder().Build();
        var query = host.Services.GetRequiredService<IServiceProviderIsService>();

        var seeder = typeof(DM.Tools.Seeder.Seeding.DataSeeder);
        var dependencies = Dependencies(seeder).ToList();

        dependencies.Should().NotBeEmpty(
            "reading no dependency of the tool would leave this rule checking nothing");

        dependencies
            .Where(dependency => !query.IsService(dependency))
            .Select(dependency => dependency.Name)
            .Should().BeEmpty(
                "the tool is composed by hand, and a dependency its container cannot " +
                "supply is neither a build error nor a startup error: it is a seeding run " +
                "that fails on the developer's machine after the database was already " +
                "reset. Registration rather than activation, because activating a store " +
                "client needs a store");
    }

    /// <remarks>
    /// Run rather than read: the host reads its own settings with one section withheld, as a
    /// deployment that lost that variable would.
    /// </remarks>
    [Fact]
    public void DemandOnlyTheConfigurationItsOwnComponentsRead()
    {
        var mail = Refusals("DM.Workers.Mail", nameof(SiteAddressConfiguration), (configuration, environment) =>
        {
            var startup = new DM.Workers.Mail.Startup(configuration, environment);
            return startup.ConfigureServices;
        });

        mail.Should().NotContain(failure => failure.Contains("SiteAddressConfiguration", StringComparison.Ordinal),
            "the mail worker renders what the message carries and builds no link of its own");

        var api = Refusals("DM.Web.API", nameof(SiteAddressConfiguration), (configuration, environment) =>
        {
            var startup = new DM.Web.API.Startup(configuration, environment);
            return startup.ConfigureServices;
        });

        api.Should().Contain(failure => failure.Contains("SiteAddressConfiguration", StringComparison.Ordinal),
            "the API builds the activation, reset and email-change links, and an empty PublicUrl " +
            "sends a reader a link that points at nothing without a word in any log");
    }

    /// <remarks>
    /// Run rather than read: each host starts on its own settings files and a deployment's
    /// encryption key, and nothing else. Mutation it must fail on: a host that still validates
    /// a section of a service the stack no longer runs.
    /// </remarks>
    [Theory]
    [InlineData("DM.Web.API")]
    [InlineData("DM.Workers.NotificationDispatcher")]
    [InlineData("DM.Workers.Mail")]
    public void StartEveryHostOnTheSettingsItsFilesCarry(string host)
    {
        Func<IConfiguration, IWebHostEnvironment, Action<IServiceCollection>> compose = host switch
        {
            "DM.Web.API" => (configuration, environment) =>
                new DM.Web.API.Startup(configuration, environment).ConfigureServices,
            "DM.Workers.NotificationDispatcher" => (configuration, environment) =>
                new DM.Workers.NotificationDispatcher.Startup(configuration, environment).ConfigureServices,
            _ => (configuration, environment) =>
                new DM.Workers.Mail.Startup(configuration, environment).ConfigureServices,
        };
        var key = KeyValuePair.Create<string, string?>(
            "CryptoConfiguration:KeyBase64", Convert.ToBase64String(new byte[32]));

        // A prefix no key starts with: nothing is withheld.
        Refusals(host, withheld: "\0", compose, key).Should().BeEmpty(
            $"{host} has to start on the settings it ships, plus the key a deployment supplies");
    }

    /// <remarks>
    /// The verifier throws on a key that is no point of the curve, which would be a 500 on every
    /// interaction. The dispatcher binds the same section and sends with the tokens alone.
    /// </remarks>
    [Fact]
    public void RefuseADiscordKeyNoSignatureVerifiesAgainstOnlyInTheHostThatVerifies()
    {
        var notAPoint = KeyValuePair.Create<string, string?>(
            "BotConfiguration:DiscordPublicKey", "02" + new string('0', 62));

        var api = Refusals("DM.Web.API", "BotConfiguration", (configuration, environment) =>
        {
            var startup = new DM.Web.API.Startup(configuration, environment);
            return startup.ConfigureServices;
        }, notAPoint);

        api.Should().Contain(failure => failure.Contains("DiscordPublicKey", StringComparison.Ordinal));

        var dispatcher = Refusals("DM.Workers.NotificationDispatcher", "BotConfiguration", (configuration, environment) =>
        {
            var startup = new DM.Workers.NotificationDispatcher.Startup(configuration, environment);
            return startup.ConfigureServices;
        }, notAPoint);

        dispatcher.Should().NotContain(failure => failure.Contains("DiscordPublicKey", StringComparison.Ordinal),
            "a host that never checks a signature has no use for the key and no reason to stop over it");
    }

    private static IReadOnlyList<string> Refusals(
        string project,
        string withheld,
        Func<IConfiguration, IWebHostEnvironment, Action<IServiceCollection>> compose,
        params KeyValuePair<string, string?>[] supplied)
    {
        var full = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(RepositoryLayout.Root, "src", project))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(full
                .AsEnumerable()
                .Where(pair => !pair.Key.StartsWith(withheld, StringComparison.OrdinalIgnoreCase))
                .Concat(supplied))
            .Build();

        var services = new ServiceCollection();
        compose(configuration, new HostEnvironment(project))(services);

        var validator = services.BuildServiceProvider().GetService<IStartupValidator>();
        if (validator == null)
        {
            return Array.Empty<string>();
        }

        try
        {
            validator.Validate();
            return Array.Empty<string>();
        }
        catch (OptionsValidationException single)
        {
            return single.Failures.ToList();
        }
        catch (AggregateException many)
        {
            return many.InnerExceptions.OfType<OptionsValidationException>()
                .SelectMany(failure => failure.Failures)
                .ToList();
        }
    }

    // The processor is resolved by type out of the message scope at runtime, inside a scope.
    [Fact]
    public void ResolveTheComponentEachWorkerRunsOn()
    {
        var entryPoints = new Dictionary<string, Type>
        {
            ["DM.Workers.NotificationDispatcher"] =
                typeof(DM.Workers.NotificationDispatcher.Startup).Assembly
                    .GetType("DM.Workers.NotificationDispatcher.Dispatching.NotificationProcessor")!,
        };

        foreach (var (host, type) in entryPoints)
        {
            type.Should().NotBeNull($"{host} must still declare the component this rule names");

            var provider = Hosts.Single(h => h.Host == host).Provider;
            using var scope = provider.CreateScope();

            var resolve = () => scope.ServiceProvider.GetRequiredService(type);
            resolve.Should().NotThrow(
                $"{host} runs on {type.Name}, and a dependency it cannot resolve is a " +
                "worker that builds, starts, reports healthy and fails on the first message");
        }
    }

    /// <remarks>
    /// The notification worker never reads the current user; a provider registered for it would
    /// answer null or Guest. Resolved in a scope, as its processor is.
    /// </remarks>
    [Fact]
    public void RefuseToNameACurrentUserInAHostThatServesNoRequests()
    {
        var provider = Hosts.Single(h => h.Host == "DM.Workers.NotificationDispatcher").Provider;
        using var scope = provider.CreateScope();

        var identity = scope.ServiceProvider.GetRequiredService<DM.Domain.Core.Identity.IIdentityProvider>();

        var read = () => identity.Current;
        read.Should().Throw<InvalidOperationException>(
            "work here comes off a queue and not out of a request, so there is no current " +
            "user; answering with a null one or with an anonymous one turns a defect in the " +
            "path into a decision made about somebody who is not there");
    }

    /// <remarks>
    /// MVC activates controllers itself, so ValidateOnBuild never walks their constructors. In a
    /// scope, as a request resolves.
    /// </remarks>
    [Fact]
    public void ResolveEveryControllerAndBackgroundJobOfEveryHost()
    {
        var unresolvable = new List<string>();
        var roots = 0;

        foreach (var (host, services, provider) in Hosts)
        {
            using var scope = provider.CreateScope();

            try
            {
                // One resolution, because the host starts them as one list.
                var jobs = scope.ServiceProvider.GetRequiredService<IEnumerable<IHostedService>>().ToList();
                roots += jobs.Count;
            }
            catch (Exception failure)
            {
                unresolvable.Add($"{host}: a hosted service - {failure.GetBaseException().Message}");
            }

            var controllers = AuthoredAssemblies(services)
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
                .Distinct()
                .ToList();

            roots += controllers.Count;
            foreach (var controller in controllers)
            {
                foreach (var dependency in Dependencies(controller))
                {
                    try
                    {
                        scope.ServiceProvider.GetRequiredService(dependency);
                    }
                    catch (Exception failure)
                    {
                        unresolvable.Add(
                            $"{host}: {controller.Name} -> {dependency.Name} - " +
                            failure.GetBaseException().Message);
                    }
                }
            }
        }

        roots.Should().BePositive(
            "a walk that found no controller and no job would report green over everything " +
            "it never resolved");
        unresolvable.Should().BeEmpty(
            "a dependency the container cannot supply is not a build error and not a startup " +
            "error: it surfaces on the first request or the first message that reaches the type");
    }

    /// <remarks>
    /// A job resolves its processor inside the timer tick, invisible to the compiler and to the rule
    /// above. The names are read out of the sources.
    /// </remarks>
    [Fact]
    public void ResolveEveryContractABackgroundJobAsksTheScopeFor()
    {
        // The namespace prefix is optional and dropped: the type is looked up by
        // name, and a qualified spelling is the same contract.
        var asked = new Regex(@"GetRequiredService<(?:[A-Za-z0-9_]+\.)*(?<name>I[A-Za-z0-9]+)>",
            RegexOptions.Compiled);

        var contracts = Directory
            .EnumerateFiles(
                Path.Combine(RepositoryLayout.Root, "src", "DM.Web.API", "HostedServices"),
                "*.cs",
                SearchOption.AllDirectories)
            .SelectMany(path => asked.Matches(File.ReadAllText(path))
                .Select(match => match.Groups["name"].Value))
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        contracts.Should().NotBeEmpty(
            "the jobs resolve their processors from the scope, and reading none of them " +
            "would report green over an empty set");

        var (_, services, provider) = Hosts.Single(host => host.Host == "DM.Web.API");
        using var scope = provider.CreateScope();

        var types = AuthoredAssemblies(services)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsInterface)
            .GroupBy(type => type.Name)
            .ToDictionary(group => group.Key, group => group.First());

        var unresolvable = new List<string>();
        foreach (var contract in contracts)
        {
            if (!types.TryGetValue(contract, out var type))
            {
                // Framework contracts are not what this rule is about.
                continue;
            }

            try
            {
                scope.ServiceProvider.GetRequiredService(type);
            }
            catch (Exception failure)
            {
                unresolvable.Add($"{contract} - {failure.GetBaseException().Message}");
            }
        }

        unresolvable.Should().BeEmpty(
            "a job that cannot resolve its processor does not fail to start: it starts, " +
            "ticks, and writes one line to the log a day while the work it exists for " +
            "is not done");
    }

    // The end-to-end check (SortRefusalShould) needs Docker; this reads the registration.
    [Fact]
    public void RegisterTheFilterThatEnforcesTheSortVocabulary()
    {
        var (_, _, provider) = Hosts.Single(host => host.Host == "DM.Web.API");

        var options = provider.GetRequiredService<IOptions<MvcOptions>>().Value;

        options.Filters
            .OfType<TypeFilterAttribute>()
            .Select(filter => filter.ImplementationType)
            .Should().Contain(typeof(DM.Web.API.Shared.Sorting.SortVocabularyFilter),
                "without it every list endpoint takes an unknown sort field, ignores it " +
                "and answers 200 with the default order - which is the answer the finding " +
                "was raised over");
    }

    /// <summary>
    /// The trap field of the forms open to guests is checked by a filter, and
    /// unregistered it lets every filled trap through.
    /// </summary>
    [Fact]
    public void RegisterTheFilterThatSpringsTheTrapOfAForm()
    {
        var (_, _, provider) = Hosts.Single(host => host.Host == "DM.Web.API");

        var options = provider.GetRequiredService<IOptions<MvcOptions>>().Value;

        options.Filters
            .OfType<TypeFilterAttribute>()
            .Select(filter => filter.ImplementationType)
            .Should().Contain(typeof(DM.Web.API.Shared.BotTrap.HoneypotFilter),
                "without it a script that fills the hidden field signs in, registers " +
                "and files tickets like a person");
    }

    internal static IReadOnlyList<(string, IServiceCollection, ServiceProvider)> BuildHosts() =>
    [
        Build("DM.Web.API", (configuration, environment) =>
        {
            var startup = new DM.Web.API.Startup(configuration, environment);
            return startup.ConfigureServices;
        }),
        Build("DM.Workers.NotificationDispatcher", (configuration, environment) =>
        {
            var startup = new DM.Workers.NotificationDispatcher.Startup(configuration, environment);
            return startup.ConfigureServices;
        }),
        Build("DM.Workers.Mail", (configuration, environment) =>
        {
            var startup = new DM.Workers.Mail.Startup(configuration, environment);
            return startup.ConfigureServices;
        }),
    ];

    private static (string, IServiceCollection, ServiceProvider) Build(
        string project,
        Func<IConfiguration, IWebHostEnvironment, Action<IServiceCollection>> compose)
    {
        // The files a Development host reads, in its order: the local credentials live in the overlay.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(RepositoryLayout.Root, "src", project))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            // A deployment supplies the encryption key; a throwaway of its shape stands in.
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CryptoConfiguration:KeyBase64"] =
                    Convert.ToBase64String(new byte[32]),
            })
            .Build();

        var services = new ServiceCollection();
        var environment = new HostEnvironment(project);

        // What the generic web host registers before ConfigureServices runs.
        var diagnostics = new DiagnosticListener(project);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton<IHostApplicationLifetime>(new NoHostLifetime());
        services.AddSingleton(diagnostics);
        services.AddSingleton<DiagnosticSource>(diagnostics);

        compose(configuration, environment)(services);

        // The same options every Program.cs passes: this build IS the startup gate.
        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        return (project, services, provider);
    }

    /// <summary>
    /// A lifetime nothing ever ends: composition under test starts no host, so
    /// the tokens never fire and stopping is nobody's business.
    /// </summary>
    private sealed class NoHostLifetime : IHostApplicationLifetime
    {
        public System.Threading.CancellationToken ApplicationStarted => default;
        public System.Threading.CancellationToken ApplicationStopping => default;
        public System.Threading.CancellationToken ApplicationStopped => default;

        public void StopApplication()
        {
        }
    }

    private static IEnumerable<string> CaptiveDependencies(IServiceCollection services) => services
        .Where(descriptor => descriptor.Lifetime == ServiceLifetime.Singleton)
        .Select(ImplementationTypeOf)
        .Where(IsAuthored)
        .Cast<Type>()
        .Distinct()
        .SelectMany(owner => Dependencies(owner)
            .Where(dependency => IsPerScope(services, dependency))
            .Select(dependency => $"{owner.Name} -> {dependency.Name}"))
        .Distinct();

    private static IEnumerable<string> UnboundConfigurationContracts(IServiceCollection services)
    {
        var registered = new HashSet<Type>(services.Select(descriptor => descriptor.ServiceType));

        return services
            .Select(ImplementationTypeOf)
            .Where(IsAuthored)
            .Cast<Type>()
            .Distinct()
            .SelectMany(Dependencies)
            .Where(dependency => dependency.IsInterface && IsAuthored(dependency) &&
                                 dependency.Name.EndsWith("Configuration", StringComparison.Ordinal))
            .Distinct()
            .Where(dependency => !registered.Contains(dependency))
            .Select(dependency => dependency.FullName!);
    }

    /// <summary>
    /// The type a descriptor constructs, as far as the descriptor says: typed
    /// registrations name it, instances carry it, factories keep it to
    /// themselves and answer null.
    /// </summary>
    private static Type? ImplementationTypeOf(ServiceDescriptor descriptor) =>
        descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();

    /// <summary>
    /// Whether a resolution of the service answers with a scoped instance: MS.DI
    /// hands it to the last descriptor of the type.
    /// </summary>
    private static bool IsPerScope(IServiceCollection services, Type service) =>
        services.LastOrDefault(descriptor => descriptor.ServiceType == service)
            ?.Lifetime == ServiceLifetime.Scoped;

    private static IEnumerable<Assembly> AuthoredAssemblies(IServiceCollection services) => services
        .Select(ImplementationTypeOf)
        .Where(IsAuthored)
        .Select(type => type!.Assembly)
        .Distinct();

    private static IEnumerable<Type> Dependencies(Type type) => type
        .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
        .SelectMany(constructor => constructor.GetParameters())
        .Select(parameter => parameter.ParameterType)
        .Where(parameterType => !parameterType.IsGenericType ||
                                !Deferred.Contains(parameterType.GetGenericTypeDefinition()))
        .Where(parameterType => !ContainerProvided.Contains(parameterType))
        .Distinct()
        .ToArray();

    private static readonly Type[] Deferred =
        [typeof(Func<>), typeof(Lazy<>), typeof(IEnumerable<>)];

    private static readonly Type[] ContainerProvided =
        [typeof(IServiceProvider), typeof(IServiceScopeFactory)];

    private static bool IsAuthored(Type? type) =>
        type is not null && type != typeof(object) &&
        type.Assembly.GetName().Name?.StartsWith("DM.", StringComparison.Ordinal) == true;

    /// <summary>
    /// Enough of an environment for composition: nothing under test reads the file
    /// providers, and the name is what the logging setup labels its stream with.
    /// </summary>
    private sealed class HostEnvironment(string applicationName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = applicationName;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
