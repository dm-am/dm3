using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A background service does its work after the host has started, not during.
/// </summary>
/// <remarks>
/// Everything before the first await of ExecuteAsync runs inside host startup. The async state
/// machine attribute is asserted: a body returning Task.CompletedTask cannot satisfy it.
/// </remarks>
public class BackgroundServiceStartupShould
{
    private static readonly IReadOnlyCollection<Type> Services = ProductionAssemblies.Types()
        .Where(type => type is { IsAbstract: false, IsClass: true })
        .Where(type => typeof(BackgroundService).IsAssignableFrom(type))
        .ToArray();

    [Fact]
    public void FindTheBackgroundServices() =>
        Services.Should().NotBeEmpty(
            "no match means the assemblies were never loaded and the rule below checks nothing");

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
}
