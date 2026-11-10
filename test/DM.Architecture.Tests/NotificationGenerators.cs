using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DM.Domain.Core.Enums;

namespace DM.Architecture.Tests;

internal static class NotificationGenerators
{
    internal static readonly Type[] Types = typeof(DM.Workers.NotificationDispatcher.Startup).Assembly.GetTypes()
        .Where(type => type is { IsAbstract: false, IsClass: true })
        .Where(type => type.GetInterfaces().Any(contract => contract.Name == "INotificationGenerator"))
        .ToArray();

    // CanResolve reads a constant or a static table, so an instance without its dependencies
    // answers.
    internal static IReadOnlyCollection<EventType> Answered(Type generator)
    {
        var canResolve = generator.GetMethod("CanResolve")!;
        var instance = RuntimeHelpers.GetUninitializedObject(generator);

        return Enum.GetValues<EventType>()
            .Where(candidate => (bool)canResolve.Invoke(instance, new object[] { candidate })!)
            .ToArray();
    }

    internal static IEnumerable<EventType> AnsweredByAny() => Types.SelectMany(Answered);
}
