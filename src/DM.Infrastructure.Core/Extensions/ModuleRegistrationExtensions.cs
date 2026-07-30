using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using Autofac.Core;
using AutoMapper;
using Microsoft.Extensions.Hosting;

namespace DM.Infrastructure.Core.Extensions;

/// <summary>
/// Extensions for autofac registration
/// </summary>
public static class ModuleRegistrationExtensions
{
    private const string RegisteredModulesKey = nameof(RegisteredModulesKey);
    private const string MapperRegisteredKey = nameof(MapperRegisteredKey);

    /// <summary>
    /// Register module, but only if not registered
    /// </summary>
    /// <param name="builder"></param>
    /// <typeparam name="TModule"></typeparam>
    /// <returns>Container builder for chaining</returns>
    public static ContainerBuilder RegisterModuleOnce<TModule>(this ContainerBuilder builder)
        where TModule : IModule, new()
    {
        var registeredModules = GetRegisteredModules(builder);

        if (registeredModules.Contains(typeof(TModule)))
        {
            return builder;
        }

        builder.RegisterModule<TModule>();
        registeredModules.Add(typeof(TModule));
        builder.Properties[RegisteredModulesKey] = registeredModules;
        return builder;
    }

    /// <summary>
    /// Register module instance and mark it as registered to prevent duplicate registration
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="module">Module instance to register</param>
    /// <typeparam name="TModule"></typeparam>
    /// <returns>Container builder for chaining</returns>
    public static ContainerBuilder RegisterModuleOnce<TModule>(this ContainerBuilder builder, TModule module)
        where TModule : IModule
    {
        var registeredModules = GetRegisteredModules(builder);

        if (registeredModules.Contains(typeof(TModule)))
        {
            return builder;
        }

        builder.RegisterModule(module);
        registeredModules.Add(typeof(TModule));
        builder.Properties[RegisteredModulesKey] = registeredModules;
        return builder;
    }

    private static HashSet<Type> GetRegisteredModules(ContainerBuilder builder)
    {
        return builder.Properties.TryGetValue(RegisteredModulesKey, out var modulesWrapper) &&
               modulesWrapper is HashSet<Type> modules
            ? modules
            : new HashSet<Type>();
    }

    /// <summary>
    /// Register default types of the calling assembly
    /// </summary>
    /// <param name="builder"></param>
    /// <returns>Container builder for chaining</returns>
    public static ContainerBuilder RegisterDefaultTypes(this ContainerBuilder builder)
        => builder.RegisterDefaultTypes(Assembly.GetCallingAssembly());

    /// <summary>
    /// Register default types from the specified assembly
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="assembly">Assembly to scan for types</param>
    /// <returns>Container builder for chaining</returns>
    public static ContainerBuilder RegisterDefaultTypes(this ContainerBuilder builder, Assembly assembly)
    {
        builder.RegisterAssemblyTypes(assembly)
            // Only non-abstract classes should be registered automatically
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t =>
            {
                // Classes that only declared private constructors should not be registered
                var declaredConstructors = t.GetDeclaredConstructors();
                return !declaredConstructors.Any() || declaredConstructors.All(d => d.IsPublic);
            })
            // Exceptions should not be registered automatically
            .Where(t => !t.IsSubclassOf(typeof(Exception)))
            .Where(t => !t.IsAssignableTo(typeof(IHostedService)))
            .AsSelf()
            .AsImplementedInterfaces()
            .InstancePerDependency()
            // Autofac modules are applied AFTER builder.Populate(services), so
            // without this the blanket scan silently overrides MS.DI
            // registrations with per-dependency construction — DbContext
            // pooling and typed-HttpClient factories were bypassed entirely.
            // The scan must only fill gaps, never replace explicit wiring.
            .PreserveExistingDefaults();

        return builder;
    }

    /// <summary>
    /// Register mappings from the calling assembly
    /// </summary>
    /// <param name="builder"></param>
    /// <returns>Container builder for chaining</returns>
    public static ContainerBuilder RegisterMapper(this ContainerBuilder builder)
        => builder.RegisterMapper(Assembly.GetCallingAssembly());

    /// <summary>
    /// Register mappings from the specified assembly
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="assembly">Assembly to scan for AutoMapper profiles</param>
    /// <returns>Container builder for chaining</returns>
    public static ContainerBuilder RegisterMapper(this ContainerBuilder builder, Assembly assembly)
    {
        builder.RegisterAssemblyTypes(assembly)
            .Where(t => t.IsClass && t.IsSubclassOf(typeof(Profile)))
            .As<Profile>();

        // Profiles accumulate per assembly; the mapper itself is one object built
        // from all of them. Registering it inside this method registered it once
        // per call — ten times in the API — and only the last registration was
        // ever resolved. The marker keeps the plumbing to exactly one.
        if (builder.Properties.ContainsKey(MapperRegisteredKey))
        {
            return builder;
        }

        builder.Properties[MapperRegisteredKey] = true;

        builder
            .Register<IConfigurationProvider>(ctx =>
                new MapperConfiguration(cfg =>
                {
                    // A null source collection stays null instead of becoming an
                    // empty one. Domain code reads that difference as "the caller
                    // did not send this field": without it a PATCH that omits a
                    // collection arrived as an empty collection, passed the
                    // `!= null` guard, and replaced the stored value with nothing.
                    cfg.AllowNullCollections = true;
                    cfg.AddProfiles(ctx.Resolve<IEnumerable<Profile>>());
                }))
            .SingleInstance();

        builder
            .Register(ctx =>
            {
                var context = ctx.Resolve<IComponentContext>();
                var configuration = context.Resolve<IConfigurationProvider>();
                return configuration.CreateMapper(context.Resolve);
            })
            .InstancePerLifetimeScope();

        return builder;
    }
}