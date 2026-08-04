using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using Autofac.Extensions.DependencyInjection;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
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
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(RepositoryRoot, "src", project))
            .AddJsonFile("appsettings.json", optional: false)
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
