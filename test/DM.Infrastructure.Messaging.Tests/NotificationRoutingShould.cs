using System;
using System.Linq;
using System.Reflection;
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
}
