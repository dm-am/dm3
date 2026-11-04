using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DM.Domain.Core.Enums;

namespace DM.Architecture.Tests;

/// <summary>
/// The notification generators of the dispatcher and the events each answers, for the rules about notifications.
/// </summary>
internal static class NotificationGenerators
{
    /// <summary>Every generator class the dispatcher assembly declares.</summary>
    internal static readonly Type[] Types = typeof(DM.Workers.NotificationDispatcher.Startup).Assembly.GetTypes()
        .Where(type => type is { IsAbstract: false, IsClass: true })
        .Where(type => type.GetInterfaces().Any(contract => contract.Name == "INotificationGenerator"))
        .ToArray();

    /// <summary>The production answer, asked the way the processor asks it.</summary>
    /// <remarks>CanResolve reads a constant or a static table, so an instance without its dependencies answers.</remarks>
    internal static IReadOnlyCollection<EventType> Answered(Type generator)
    {
        var canResolve = generator.GetMethod("CanResolve")!;
        var instance = RuntimeHelpers.GetUninitializedObject(generator);

        return Enum.GetValues<EventType>()
            .Where(candidate => (bool)canResolve.Invoke(instance, new object[] { candidate })!)
            .ToArray();
    }

    /// <summary>Every event some generator answers.</summary>
    internal static IEnumerable<EventType> AnsweredByAny() => Types.SelectMany(Answered);
}
