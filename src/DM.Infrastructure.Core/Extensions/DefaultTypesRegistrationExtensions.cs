using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DM.Infrastructure.Core.Extensions;

/// <summary>
/// The assembly scan every module builds its registrations on.
/// </summary>
/// <remarks>
/// Ported from the Autofac scan with its semantics kept on purpose. The scan
/// only fills gaps and never replaces explicit wiring: MS.DI hands a single
/// resolution to the last descriptor of a service type, so a blanket scan
/// registering over AddDbContextPool or a typed HttpClient factory would
/// silently win - the same class of bug PreserveExistingDefaults closed on the
/// Autofac side. A service type registered before the scans stays theirs;
/// service types the scans themselves introduce stay open, so a second assembly
/// still adds its implementations of a shared contract to the same enumerable
/// (both S3 client providers must survive, which is also why this is not
/// Scrutor: its skip strategy silences by service type alone).
///
/// Interfaces forward to the self registration rather than name the type twice:
/// when a module pins the implementation type to a lifetime of its own, a
/// resolution through the interface follows that registration instead of
/// constructing a rogue per-dependency copy.
/// </remarks>
public static class DefaultTypesRegistrationExtensions
{
    /// <summary>
    /// Register default types from the specified assembly: every activatable
    /// class as itself and its interfaces, transient, without displacing
    /// anything registered explicitly.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="assembly">Assembly to scan for types</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddDefaultTypes(this IServiceCollection services, Assembly assembly)
    {
        var scanIntroduced = ScanIntroducedServiceTypes(services);
        var registeredBefore = new HashSet<Type>(services.Select(descriptor => descriptor.ServiceType));

        foreach (var type in assembly.GetTypes().Where(IsDefaultRegistrationCandidate))
        {
            foreach (var descriptor in Contracts(type))
            {
                if (registeredBefore.Contains(descriptor.ServiceType) &&
                    !scanIntroduced.Contains(descriptor.ServiceType))
                {
                    // Registered outside the scans - the scan leaves it alone.
                    continue;
                }

                // By (service, implementation) pair: the same pair twice is a
                // repeat of the same scan, a new pair is another implementation.
                // TryAddEnumerable refuses the self pair by design - the service
                // type is its own implementation - so that one is deduped by hand.
                if (descriptor.ServiceType == type)
                {
                    if (!services.Any(existing => existing.ServiceType == type &&
                                                  existing.ImplementationType == type))
                    {
                        services.Add(descriptor);
                    }
                }
                else
                {
                    services.TryAddEnumerable(descriptor);
                }

                scanIntroduced.Add(descriptor.ServiceType);
            }
        }

        return services;
    }

    private static bool IsDefaultRegistrationCandidate(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
        !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) &&
        !typeof(Delegate).IsAssignableFrom(type) &&
        // Exceptions should not be registered automatically
        !type.IsSubclassOf(typeof(Exception)) &&
        // Attributes are instantiated by reflection over the members that wear
        // them, never by the container
        !type.IsSubclassOf(typeof(Attribute)) &&
        // Hosted services are started by the host, through AddHostedService only
        !typeof(IHostedService).IsAssignableFrom(type) &&
        // A scanned DbContext would shadow AddDbContextPool - the pooled scoped
        // registration must stay the only one. By name, because this assembly
        // has no reason to reference EF Core for one exclusion.
        !IsDbContext(type) &&
        // Classes that declared any non-public constructor opted out of the scan
        HasOnlyPublicDeclaredConstructors(type) &&
        HasActivatableConstructor(type);

    private static bool IsDbContext(Type type)
    {
        for (var current = type.BaseType; current != null; current = current.BaseType)
        {
            if (current.FullName == "Microsoft.EntityFrameworkCore.DbContext")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOnlyPublicDeclaredConstructors(Type type)
    {
        var declared = type.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return declared.Length == 0 || declared.All(constructor => constructor.IsPublic);
    }

    /// <summary>
    /// Whether any public constructor could ever be satisfied by a container.
    /// </summary>
    /// <remarks>
    /// The scan sweeps up types nobody resolves - positional records, results,
    /// tokens - whose constructors take strings and value types no container
    /// supplies. Under Autofac those registrations sat inert; under
    /// ValidateOnBuild every one is a startup failure. A type none of whose
    /// constructors could be satisfied is excluded: resolving it failed under
    /// Autofac too, this only moves the refusal out of the container.
    /// </remarks>
    private static bool HasActivatableConstructor(Type type) =>
        type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Any(constructor => constructor.GetParameters()
                .All(parameter => parameter.HasDefaultValue || IsInjectable(parameter.ParameterType)));

    private static bool IsInjectable(Type parameterType) =>
        (parameterType.IsInterface || (parameterType.IsClass && parameterType != typeof(string))) &&
        // MS.DI answers IEnumerable, not arrays, and no delegate is a service
        !parameterType.IsArray &&
        !typeof(Delegate).IsAssignableFrom(parameterType);

    private static IEnumerable<ServiceDescriptor> Contracts(Type implementation)
    {
        yield return ServiceDescriptor.Transient(implementation, implementation);

        foreach (var contract in implementation.GetInterfaces()
                     .Where(contract => contract != typeof(IDisposable) &&
                                        contract != typeof(IAsyncDisposable)))
        {
            yield return Forward(contract, implementation);
        }
    }

    private static readonly MethodInfo ForwardDefinition = typeof(DefaultTypesRegistrationExtensions)
        .GetMethod(nameof(ForwardCore), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static ServiceDescriptor Forward(Type service, Type implementation) =>
        (ServiceDescriptor)ForwardDefinition.MakeGenericMethod(service, implementation).Invoke(null, null)!;

    // The generic factory overload on purpose: it keeps the implementation type
    // on the descriptor, which is what TryAddEnumerable dedupes by.
    private static ServiceDescriptor ForwardCore<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService =>
        ServiceDescriptor.Transient<TService, TImplementation>(
            provider => provider.GetRequiredService<TImplementation>());

    /// <summary>
    /// Which service types the scans themselves introduced, carried in the
    /// collection so that every scan over any assembly shares one answer.
    /// </summary>
    private static HashSet<Type> ScanIntroducedServiceTypes(IServiceCollection services)
    {
        var holder = services
            .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(ScanIntroducedTypes))
            ?.ImplementationInstance as ScanIntroducedTypes;
        if (holder == null)
        {
            holder = new ScanIntroducedTypes();
            services.AddSingleton(holder);
        }

        return holder.Types;
    }

    private sealed class ScanIntroducedTypes
    {
        public HashSet<Type> Types { get; } = [];
    }
}
