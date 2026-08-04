using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using Autofac.Extensions.DependencyInjection;
using DM.Domain.Core.Configuration;
using FluentAssertions;
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
/// The seeder is absent on purpose: it composes inside a private method of its
/// entry point, so the third rule reads its source instead.
/// </remarks>
public class CompositionRootShould
{
    private static readonly IReadOnlyList<(string Host, IContainer Container)> Hosts = BuildHosts();

    [Fact]
    public void BuildTheContainerOfEveryHostItCovers()
    {
        Hosts.Should().HaveCount(3);
        foreach (var (host, container) in Hosts)
        {
            container.ComponentRegistry.Registrations.Should().HaveCountGreaterThan(100,
                $"{host} registers a whole application, and a near-empty graph means the " +
                "rules below check nothing");
        }
    }

    [Fact]
    public void KeepEveryProcessWideComponentFreeOfScopedDependencies()
    {
        foreach (var (host, container) in Hosts)
        {
            CaptiveDependencies(container).Should().BeEmpty(
                $"in {host} a component activated once in the root scope keeps whatever it " +
                "was given for the life of the process, so a scoped dependency it takes is " +
                "never released and never refreshed - down to the pooled DbContext behind " +
                "authentication");
        }
    }

    [Fact]
    public void RegisterEveryConfigurationContractItsOwnComponentsAskFor()
    {
        foreach (var (host, container) in Hosts)
        {
            UnboundConfigurationContracts(container).Should().BeEmpty(
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

        if (!program.Contains("typeof(ISecurityManager).Assembly", StringComparison.Ordinal))
        {
            return;
        }

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
    /// </remarks>
    [Fact]
    public void SupplyEveryDependencyOfTheSeederOutOfTheContainerTheToolRunsOn()
    {
        using var host = DM.Tools.Seeder.Program.CreateHostBuilder().Build();
        var container = host.Services.GetRequiredService<ILifetimeScope>();

        var seeder = typeof(DM.Tools.Seeder.Seeding.DataSeeder);
        var dependencies = Dependencies(seeder).ToList();

        dependencies.Should().HaveCountGreaterThan(5,
            "the tool takes both stores, the hashing, the identifiers and the popularity " +
            "processors, and reading none of them would leave this rule checking nothing");

        dependencies
            .Where(dependency => !container.IsRegistered(dependency))
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
    /// The shared configuration call demanded IntegrationSettings:WebUrl from
    /// everyone, so the mail worker — which speaks SMTP, touches no store and
    /// builds no link, the three senders that do being in an assembly it does not
    /// scan — refused to start over a value it never reads: "OptionsValidation
    /// Exception: IntegrationSettings:WebUrl is required. Hosting failed to
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
        var mail = Refusals("DM.Workers.Mail", nameof(IntegrationSettings), (configuration, environment) =>
        {
            var startup = new DM.Workers.Mail.Startup(configuration, environment);
            return startup.ConfigureServices;
        });

        mail.Should().NotContain(failure => failure.Contains("IntegrationSettings", StringComparison.Ordinal),
            "the mail worker renders what the message carries and builds no link of its own");

        var api = Refusals("DM.Web.API", nameof(IntegrationSettings), (configuration, environment) =>
        {
            var startup = new DM.Web.API.Startup(configuration, environment);
            return startup.ConfigureServices;
        });

        api.Should().Contain(failure => failure.Contains("IntegrationSettings", StringComparison.Ordinal),
            "the API builds the activation, reset and email-change links, and an empty WebUrl " +
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
    /// A container builds whether or not its graph can be walked: Autofac finds a
    /// missing registration when something asks for it, which for a worker is on a
    /// live message. So a dependency added to a domain service compiles, the
    /// container still builds, the whole tier stays green, and the queue starts
    /// failing on the first event.
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
                    .GetType("DM.Workers.NotificationDispatcher.Implementation.NotificationProcessor")!,
        };

        foreach (var (host, type) in entryPoints)
        {
            type.Should().NotBeNull($"{host} must still declare the component this rule names");

            var container = Hosts.Single(h => h.Host == host).Container;
            using var scope = container.BeginLifetimeScope();

            var resolve = () => scope.Resolve(type);
            resolve.Should().NotThrow(
                $"{host} runs on {type.Name}, and a dependency it cannot resolve is a " +
                "worker that builds, starts, reports healthy and fails on the first message");
        }
    }

    /// <summary>
    /// Every controller and every background job of a host resolves.
    /// </summary>
    /// <remarks>
    /// The third thing this class was asked for and the one it never did. The
    /// static walk above sees one kind of missing registration — a contract whose
    /// name ends in Configuration — and nothing else: a repository, a generator or
    /// a domain service that nobody registered is invisible to it, because a
    /// container builds whether or not its graph can be walked. Autofac finds that
    /// on the first request that reaches the controller, which is production for
    /// an endpoint nobody opens in review.
    ///
    /// Controllers and hosted services because they are the roots: everything the
    /// host can reach is under one of them. In a scope, since that is where a
    /// request resolves and per-scope registrations are not resolvable from the
    /// root.
    ///
    /// A controller is resolved through its dependencies rather than as itself,
    /// because MVC activates controllers with its own activator and the container
    /// never holds the type — what the container is asked for is exactly the list
    /// of constructor parameters, which is what a request asks it for too.
    /// </remarks>
    [Fact]
    public void ResolveEveryControllerAndBackgroundJobOfEveryHost()
    {
        var unresolvable = new List<string>();
        var roots = 0;

        foreach (var (host, container) in Hosts)
        {
            using var scope = container.BeginLifetimeScope();

            try
            {
                // One resolution, because the host starts them as one list.
                var jobs = scope.Resolve<IEnumerable<IHostedService>>().ToList();
                roots += jobs.Count;
            }
            catch (Exception failure)
            {
                unresolvable.Add($"{host}: a hosted service - {failure.GetBaseException().Message}");
            }

            var controllers = container.ComponentRegistry.Registrations
                .Select(registration => registration.Activator.LimitType)
                .Where(IsAuthored)
                .Select(type => type.Assembly)
                .Distinct()
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
                        scope.Resolve(dependency);
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

        var (_, container) = Hosts.Single(host => host.Host == "DM.Web.API");
        using var scope = container.BeginLifetimeScope();

        var types = container.ComponentRegistry.Registrations
            .Select(registration => registration.Activator.LimitType)
            .Where(IsAuthored)
            .Select(type => type.Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsInterface)
            .GroupBy(type => type.Name)
            .ToDictionary(group => group.Key, group => group.First());

        var unresolvable = new List<string>();
        foreach (var contract in contracts)
        {
            if (!types.TryGetValue(contract, out var type))
            {
                // Framework contracts (IMapper, IDateTimeProvider and the like) are
                // not what this rule is about; the ones it is about live in the
                // assemblies the host registers.
                continue;
            }

            try
            {
                scope.Resolve(type);
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
        var (_, container) = Hosts.Single(host => host.Host == "DM.Web.API");

        var options = container.Resolve<IOptions<MvcOptions>>().Value;

        options.Filters
            .OfType<TypeFilterAttribute>()
            .Select(filter => filter.ImplementationType)
            .Should().Contain(typeof(DM.Web.API.Shared.Sorting.SortVocabularyFilter),
                "without it every list endpoint takes an unknown sort field, ignores it " +
                "and answers 200 with the default order - which is the answer the finding " +
                "was raised over");
    }

    private static IReadOnlyList<(string, IContainer)> BuildHosts() =>
    [
        ("DM.Web.API", Build("DM.Web.API", (configuration, environment) =>
        {
            var startup = new DM.Web.API.Startup(configuration, environment);
            return (startup.ConfigureServices, startup.ConfigureContainer);
        })),
        ("DM.Workers.NotificationDispatcher", Build("DM.Workers.NotificationDispatcher", (configuration, environment) =>
        {
            var startup = new DM.Workers.NotificationDispatcher.Startup(configuration, environment);
            return (startup.ConfigureServices, startup.ConfigureContainer);
        })),
        ("DM.Workers.Mail", Build("DM.Workers.Mail", (configuration, environment) =>
        {
            var startup = new DM.Workers.Mail.Startup(configuration, environment);
            return (startup.ConfigureServices, startup.ConfigureContainer);
        })),
    ];

    private static IContainer Build(
        string project,
        Func<IConfiguration, IWebHostEnvironment, (Action<IServiceCollection>, Action<ContainerBuilder>)> compose)
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

        var (configureServices, configureContainer) = compose(configuration, new HostEnvironment(project));

        var services = new ServiceCollection();
        configureServices(services);

        var builder = new ContainerBuilder();
        builder.Populate(services);
        configureContainer(builder);
        return builder.Build();
    }

    private static IEnumerable<string> CaptiveDependencies(IContainer container) => container
        .ComponentRegistry.Registrations
        .Where(registration => registration.Sharing == InstanceSharing.Shared &&
                               registration.Lifetime is RootScopeLifetime)
        .Select(registration => registration.Activator.LimitType)
        .Where(IsAuthored)
        .Distinct()
        .SelectMany(owner => Dependencies(owner)
            .Where(dependency => IsPerScope(container, dependency))
            .Select(dependency => $"{owner.Name} -> {dependency.Name}"))
        .Distinct();

    private static IEnumerable<string> UnboundConfigurationContracts(IContainer container) => container
        .ComponentRegistry.Registrations
        .Select(registration => registration.Activator.LimitType)
        .Where(IsAuthored)
        .Distinct()
        .SelectMany(Dependencies)
        .Where(dependency => dependency.IsInterface && IsAuthored(dependency) &&
                             dependency.Name.EndsWith("Configuration", StringComparison.Ordinal))
        .Distinct()
        .Where(dependency => !container.IsRegistered(dependency))
        .Select(dependency => dependency.FullName!);

    private static bool IsPerScope(IContainer container, Type service) =>
        container.ComponentRegistry.TryGetRegistration(new TypedService(service), out var registration) &&
        registration.Sharing == InstanceSharing.Shared &&
        registration.Lifetime is CurrentScopeLifetime;

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
        [typeof(IServiceProvider), typeof(ILifetimeScope), typeof(IComponentContext), typeof(IServiceScopeFactory)];

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

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !(Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                     Directory.Exists(Path.Combine(directory.FullName, "test"))))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }
}
