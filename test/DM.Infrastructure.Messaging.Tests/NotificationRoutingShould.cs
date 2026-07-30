using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using DM.Domain.Core.Enums;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// The dispatcher binds its queue to exactly the events its generators answer,
/// asking each one through CanResolve. That binding used to be a hand-written
/// list and drifted: 14 generators answered events no binding matched, so
/// RabbitMQ dropped those messages and the features simply looked absent.
///
/// Deriving the binding makes that drift impossible, but only while every
/// generator is actually discoverable and actually answers something — which is
/// what these tests hold in place.
/// </summary>
public class NotificationRoutingShould
{
    private static Type[] GeneratorTypes => Assembly
        .Load("DM.Workers.NotificationDispatcher")
        .GetTypes()
        .Where(t => t is { IsAbstract: false, IsClass: true })
        .Where(t => t.GetInterfaces().Any(i => i.Name == "INotificationGenerator"))
        .ToArray();

    [Fact]
    public void ExposeGeneratorsToDeriveTheBindingFrom()
    {
        GeneratorTypes.Should().HaveCountGreaterThan(20,
            "the queue binding is derived from these types; discovering none would bind nothing at all, " +
            "and the dispatcher would go quiet without a single error");
    }

    /// <summary>
    /// A generator answers either through the single-event property of the base
    /// class or by overriding CanResolve itself (the character-status generator
    /// covers a whole family of events that way). One of the two must be true,
    /// otherwise the type is registered, resolved, and never triggered.
    /// </summary>
    [Fact]
    public void AnswerAtLeastOneEvent()
    {
        var silent = GeneratorTypes
            .Where(type =>
            {
                var declaresEvent = type.GetProperty("EventType",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
                    BindingFlags.FlattenHierarchy) is not null;
                var overridesCanResolve = type.GetMethod("CanResolve",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) is not null;
                return !declaresEvent && !overridesCanResolve;
            })
            .Select(type => type.Name)
            .ToArray();

        silent.Should().BeEmpty("a generator that answers no event can never produce a notification");
    }

    /// <summary>
    /// The unread-messages badge has nothing else behind it: the client refreshes
    /// it when this event arrives over the hub, and no timer stands in for that.
    /// The event reaches the hub only while some generator answers it — what no
    /// generator answers is not in the binding, and the broker drops it without
    /// a trace. Deleting that generator would switch the live badge off and leave
    /// every other test green.
    /// </summary>
    [Fact]
    public void BindTheEventTheUnreadBadgeRidesOn()
    {
        GeneratorTypes.Any(type => Answers(type, EventType.NewMessage)).Should().BeTrue(
            "a new message reaches an open tab through this event and no other");
    }

    /// <summary>
    /// Asks a generator the question the dispatcher asks it at startup. The
    /// constructor is not run: it wants a database context, while CanResolve
    /// reads constants only. The method is reached by name because the interface
    /// is internal to the worker and this suite is not on its InternalsVisibleTo
    /// list.
    /// </summary>
    private static bool Answers(Type generatorType, EventType eventType)
    {
        var canResolve = generatorType.GetMethod("CanResolve", new[] { typeof(EventType) });
        canResolve.Should().NotBeNull($"{generatorType.Name} is asked this by the dispatcher");
        var generator = RuntimeHelpers.GetUninitializedObject(generatorType);
        return (bool)canResolve!.Invoke(generator, new object[] { eventType })!;
    }
}
