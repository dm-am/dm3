using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DM.Domain.Core.Configuration;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The container each host builds is held to the two contracts nothing else checks.
/// </summary>
/// <remarks>
/// Nothing built a container under test, so a process-wide component holding a
/// scoped one - the connection tracker holding a pooled DbContext for the life of
/// the process - reached production, and a host that registered a domain assembly
/// without binding its options failed on a live message instead of at startup.
/// Both are properties of the registration graph, visible without activating
/// anything, which is why this builds the containers rather than the hosts.
///
/// The providers are built with ValidateScopes and ValidateOnBuild - the same
/// options every Program.cs passes - so what this class exercises is the very
/// startup gate the hosts run: a descriptor the container cannot satisfy fails
/// the build here exactly as it would fail the deployment.
///
/// The seeder is absent on purpose: it composes inside a private method of its
/// entry point, so the third rule reads its source instead.
/// </remarks>
public class CompositionRootShould
{
    private static readonly IReadOnlyList<(string Host, IServiceCollection Services, ServiceProvider Provider)>
        Hosts = BuildHosts();

    [Fact]
    public void BuildTheContainerOfEveryHostItCovers()
    {
        Hosts.Should().HaveCount(3);
        foreach (var (host, services, _) in Hosts)
        {
            services.Should().HaveCountGreaterThan(100,
                $"{host} registers a whole application, and a near-empty graph means the " +
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
            RepositoryRoot, "src", "DM.Tools.Seeder", "Program.cs"));

        program.Should().Contain("typeof(ISecurityManager).Assembly",
            "the assertion below is about the assembly this marker names, so a seeder that " +
            "scans another one has to be seen rather than silently excused");

        program.Should().Contain("AddDmAccountConfiguration",
            "the tool scans the whole account assembly, whose types read four option " +
            "sections; IOptions of an unbound type hands out a default rather than " +
            "throwing, so the encryption key would be empty at the first call that " +
            "needs it instead of missing at startup");
    }

    /// <summary>
    /// The seeder resolves the one component it runs on.
    /// </summary>
    /// <remarks>
    /// It composes inside a private method of its entry point, so this class could
    /// only read that file as text and never resolve anything out of it — which
    /// left the tool the one executable whose container nothing built. It matters
    /// now that the fixture scores its games and blogs through the site's own
    /// popularity processors rather than through a copy of the calculation: those
    /// classes are internal to their modules, so what makes them resolvable is a
    /// pair of assembly scans and nothing the compiler checks. Without this, a
    /// missing scan surfaces as a seeding run that dies before it writes a row.
    ///
    /// Building the host is itself half the rule: the seeder passes
    /// ValidateOnBuild, so a descriptor its container cannot satisfy throws here.
    /// </remarks>
    [Fact]
    public void SupplyEveryDependencyOfTheSeederOutOfTheContainerTheToolRunsOn()
    {
        using var host = DM.Tools.Seeder.Program.CreateHostBuilder().Build();
        var query = host.Services.GetRequiredService<IServiceProviderIsService>();

        var seeder = typeof(DM.Tools.Seeder.Seeding.DataSeeder);
        var dependencies = Dependencies(seeder).ToList();

        dependencies.Should().HaveCountGreaterThan(5,
            "the tool takes both stores, the hashing, the identifiers and the popularity " +
            "processors, and reading none of them would leave this rule checking nothing");

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

    /// <summary>
    /// A host refuses to start over the values its own components read, and over
    /// no others.
    /// </summary>
    /// <remarks>
    /// The shared configuration call demanded SiteAddressConfiguration:PublicUrl from
    /// everyone, so the mail worker — which speaks SMTP, touches no store and
    /// builds no link, the three senders that do being in an assembly it does not
    /// scan — refused to start over a value it never reads: "OptionsValidation
    /// Exception: SiteAddressConfiguration:PublicUrl is required. Hosting failed to
    /// start." That is the same defect the Require* split was made to end, left
    /// behind in the one demand that was not moved, and its cost is the next host
    /// writing in a fake value to get past it.
    ///
    /// Run rather than read: ValidateOnStart hands its predicates to
    /// IStartupValidator, and running that is exactly what the host does before it
    /// serves anything. The host reads its own settings with one section withheld,
    /// which is what a deployment that lost that variable gives it, so the
    /// refusals it produces are the demands that host actually makes.
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

    /// <summary>
    /// What a host refuses to start over when <paramref name="withheld" /> is
    /// absent, everything else being what it reads on a local run.
    /// </summary>
    private static IReadOnlyList<string> Refusals(
        string project,
        string withheld,
        Func<IConfiguration, IWebHostEnvironment, Action<IServiceCollection>> compose)
    {
        var full = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(RepositoryRoot, "src", project))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(full
                .AsEnumerable()
                .Where(pair => !pair.Key.StartsWith(withheld, StringComparison.OrdinalIgnoreCase)))
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

    /// <summary>
    /// The component each worker exists to run resolves, with everything under it.
    /// </summary>
    /// <remarks>
    /// ValidateOnBuild already proves every registered descriptor constructible,
    /// but the processor is resolved by type out of the message scope at runtime
    /// - GetRequiredService inside the consumer - and a registration nothing
    /// removed is still what this rule pins: delete the scan that supplies it
    /// and only this fails with the worker's name on it.
    ///
    /// Resolved inside a scope, because that is where the hosted service resolves
    /// its processor, and per-scope registrations are not resolvable from the root.
    /// </remarks>
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

    /// <summary>
    /// A host with no requests has no current user, and says so.
    /// </summary>
    /// <remarks>
    /// The notification worker declares this dependency because the notification
    /// service asks for it, and it never reaches a path that reads it. It used to
    /// be satisfied by scanning the whole account domain in — for that one
    /// interface — which registered a provider whose backing field is null until a
    /// request sets it, and an authorization context that answers Guest.
    ///
    /// Two failures nothing would have reported. A path arriving at the provider
    /// gets a null reference from a property that promises an identity; a path
    /// arriving at authorization is answered as an anonymous visitor, which is a
    /// real answer to a question that has no reader behind it. Both are quiet, and
    /// the second is quiet on the side that grants rather than refuses.
    ///
    /// Resolved in a scope because that is where the processor of this host is
    /// resolved.
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

    /// <summary>
    /// Every controller and every background job of a host resolves.
    /// </summary>
    /// <remarks>
    /// Controllers are the blind spot ValidateOnBuild keeps: MVC activates them
    /// with its own activator, so the container never holds the type and the
    /// startup gate never walks their constructors. What the container is asked
    /// for at runtime is exactly the list of constructor parameters, which is
    /// what this resolves. The hosted services are resolved as one list because
    /// that is how the host starts them.
    ///
    /// In a scope, since that is where a request resolves and per-scope
    /// registrations are not resolvable from the root.
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

        roots.Should().BeGreaterThan(50,
            "the hosts between them run some seventy controllers and a dozen jobs, and a walk " +
            "that found a handful would report green over everything it never resolved");
        unresolvable.Should().BeEmpty(
            "a dependency the container cannot supply is not a build error and not a startup " +
            "error: it surfaces on the first request or the first message that reaches the type");
    }

    /// <summary>
    /// Every contract a background job asks the scope for is one the container can
    /// supply.
    /// </summary>
    /// <remarks>
    /// The rule above resolves the jobs themselves, and that used to be the whole
    /// story: a job took its dependencies in its constructor, so resolving the job
    /// walked the graph behind it. It stopped being the whole story when the jobs
    /// were moved off the DbContext -- each one now takes IServiceProvider and a
    /// logger and resolves its processor inside the timer tick, so a processor with
    /// no registration is invisible to the compiler, invisible to the rule above,
    /// and surfaces once a day as one LogError inside PeriodicHostedService.Pass.
    /// Deleting the TokenMaintenanceRepository registration left all 192
    /// architecture tests green while token retention was dead.
    ///
    /// The names are read out of the sources rather than listed here: a list would
    /// have to be extended by the same person who forgets the registration.
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
                Path.Combine(RepositoryRoot, "src", "DM.Web.API", "HostedServices"),
                "*.cs",
                SearchOption.AllDirectories)
            .SelectMany(path => asked.Matches(File.ReadAllText(path))
                .Select(match => match.Groups["name"].Value))
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        contracts.Should().HaveCountGreaterThan(5,
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
                // Framework contracts (IServiceScopeFactory and the like) are
                // not what this rule is about; the ones it is about live in the
                // assemblies the host registers.
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

    /// <summary>
    /// The sort vocabulary is enforced by a filter, and a filter enforces
    /// nothing until it is registered.
    /// </summary>
    /// <remarks>
    /// One line in Startup stands between "?sortBy=nonsense answers 400" and a
    /// table plus a filter plus a Swagger extension that are all dead code. The
    /// rule that catches its absence end to end needs Docker and Testcontainers
    /// (SortRefusalShould), which is not something every checkout runs, and the
    /// line went missing once already. This reads the same registration out of
    /// the options the host composes, and needs neither.
    /// </remarks>
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

    private static IReadOnlyList<(string, IServiceCollection, ServiceProvider)> BuildHosts() =>
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
        // The same two files a Development host reads, in the same order: the
        // environment declared below is Development, and the credentials a local
        // run needs live in the overlay rather than in the tracked base file, so
        // reading only the base one composes a host no environment ever runs.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(RepositoryRoot, "src", project))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            // The encryption key has no answer in the repository on purpose — a
            // deployment supplies it — and five services validate it while being
            // activated. A throwaway of the right shape stands in for it, so what
            // this class reports is a graph that cannot be walked rather than a
            // value a test environment was never given.
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CryptoConfiguration:KeyBase64"] =
                    Convert.ToBase64String(new byte[32]),
            })
            .Build();

        var services = new ServiceCollection();
        var environment = new HostEnvironment(project);

        // What the real host registers before ConfigureServices ever runs: the
        // generic web host puts the configuration, the environment, the
        // lifetime and the diagnostics source in first. Composed here too, so
        // that ValidateOnBuild judges the host's own registrations rather than
        // the absence of the host around them.
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

    /// <summary>
    /// Constructor parameter types of a component, minus the ones the container
    /// supplies from itself and the wrappers that defer or fan out a resolution.
    /// </summary>
    private static IEnumerable<Type> Dependencies(Type type)
    {
        try
        {
            return type
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Where(parameterType => !parameterType.IsGenericType ||
                                        !Deferred.Contains(parameterType.GetGenericTypeDefinition()))
                .Where(parameterType => !ContainerProvided.Contains(parameterType))
                .Distinct()
                .ToArray();
        }
        catch (Exception)
        {
            // A parameter whose type cannot be loaded is not a dependency this can judge
            return Array.Empty<Type>();
        }
    }

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

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
