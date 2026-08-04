using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A background service does its work after the host has started, not during.
/// </summary>
/// <remarks>
/// BackgroundService.StartAsync calls ExecuteAsync and awaits nothing: everything
/// before the first await runs inside host startup. Both worker consumers declared
/// ExecuteAsync as a plain Task and opened a connection to the broker in it, so a
/// broker that was not up yet aborted the host before its own health check could
/// say why - and the retry policy first held the start for a minute of Thread.Sleep.
/// The API consumer avoided it with a Task.Yield and a comment describing exactly
/// this, which is the asymmetry the rule removes.
///
/// An async state machine is what makes the yield possible, so the attribute the
/// compiler emits for one is what gets asserted: it cannot be satisfied by a body
/// that returns Task.CompletedTask after doing the work.
/// </remarks>
public class BackgroundServiceStartupShould
{
    private static readonly IReadOnlyCollection<Type> Services = ProductionTypes()
        .Where(type => type is { IsAbstract: false, IsClass: true })
        .Where(type => typeof(BackgroundService).IsAssignableFrom(type))
        .ToArray();

    [Fact]
    public void FindTheBackgroundServices() =>
        Services.Should().HaveCountGreaterOrEqualTo(10,
            "the API host alone runs nine periodic jobs, so a smaller match means the " +
            "assemblies were never loaded and the rule below checks nothing");

    [Fact]
    public void RunEveryExecuteAsyncAsAnAsyncMethod() =>
        Services
            .Select(type => (Type: type, Method: type.GetMethod(
                "ExecuteAsync",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy)))
            .Where(candidate => candidate.Method is not null)
            .Where(candidate => candidate.Method!.GetCustomAttribute<AsyncStateMachineAttribute>() is null)
            .Select(candidate => candidate.Type.FullName)
            .Should().BeEmpty(
                "a synchronous body runs inside StartAsync, so anything it touches that is " +
                "not up yet takes the whole host down before it ever finishes starting");

    private static IEnumerable<Type> ProductionTypes() => Directory
        .EnumerateFiles(AppContext.BaseDirectory, "DM.*.dll", SearchOption.TopDirectoryOnly)
        .Where(path => !Path.GetFileNameWithoutExtension(path)
            .EndsWith(".Tests", StringComparison.Ordinal))
        .SelectMany(TypesOf);

    private static IEnumerable<Type> TypesOf(string assemblyPath)
    {
        try
        {
            return Assembly.LoadFrom(assemblyPath).GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(type => type is not null).Select(type => type!);
        }
        catch (Exception)
        {
            return Array.Empty<Type>();
        }
    }
}
