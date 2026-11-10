using System;
using System.Linq;
using System.Runtime.CompilerServices;
using DM.Domain.Core.Enums;
using DM.Workers.NotificationDispatcher.Notifiers;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// An event becomes a notification only through a generator that answers it, asked
/// through CanResolve. Fourteen generators once answered nothing, and the features
/// simply looked absent. These tests hold every generator discoverable and answering.
/// </summary>
public class NotificationRoutingShould
{
    private static Type[] GeneratorTypes => typeof(INotificationGenerator).Assembly
        .GetTypes()
        .Where(t => t is { IsAbstract: false, IsClass: true })
        .Where(typeof(INotificationGenerator).IsAssignableFrom)
        .ToArray();

    [Fact]
    public void ExposeTheGeneratorsTheProcessorAsks()
    {
        GeneratorTypes.Should().HaveCountGreaterThan(20,
            "the processor asks these types; discovering none would answer no event at all, " +
            "and the dispatcher would go quiet without a single error");
    }

    /// <summary>
    /// A generator answers at least one event, asked the way the dispatcher asks.
    /// </summary>
    /// <remarks>
    /// This used to look for the members rather than call them: GetProperty walks
    /// the whole hierarchy, so the abstract EventType of the base class answered
    /// for every subclass, and a type implementing the interface directly has to
    /// declare CanResolve to compile. The set was empty whatever the code did.
    /// Calling CanResolve over the whole enum is the question the dispatcher
    /// asks at startup, and a generator whose answer is always false is exactly
    /// what shipped fourteen times.
    /// </remarks>
    [Fact]
    public void AnswerAtLeastOneEvent()
    {
        var silent = GeneratorTypes
            .Where(type => !Enum.GetValues<EventType>().Any(eventType => Answers(type, eventType)))
            .Select(type => type.Name)
            .ToArray();

        silent.Should().BeEmpty("a generator that answers no event can never produce a notification");
    }

    /// <summary>
    /// The unread-messages badge has nothing else behind it: the client refreshes
    /// it when this event arrives over the hub, and no timer stands in for that.
    /// The event reaches the hub only while some generator answers it, and what no
    /// generator answers is marked processed without a trace. Deleting that
    /// generator would switch the live badge off and leave every other test green.
    /// </summary>
    [Fact]
    public void AnswerTheEventTheUnreadBadgeRidesOn()
    {
        GeneratorTypes.Any(type => Answers(type, EventType.NewMessage)).Should().BeTrue(
            "a new message reaches an open tab through this event and no other");
    }

    /// <summary>
    /// The events strip above the chat feed has nothing else behind it either: it
    /// is read when the page opens and no timer re-reads it. A start or an end
    /// reaches an open tab through these two events and nothing else, and an
    /// event no generator answers is marked processed without a trace, so the strip
    /// goes on showing what was true at page load.
    /// </summary>
    [Theory]
    [InlineData(EventType.GlobalChatEventStarted)]
    [InlineData(EventType.GlobalChatEventEnded)]
    public void AnswerTheEventsTheChatEventsStripRidesOn(EventType eventType) =>
        GeneratorTypes.Any(type => Answers(type, eventType)).Should().BeTrue(
            "the strip re-reads itself on this event and on no other");

    /// <summary>
    /// Asks a generator the question the dispatcher asks it at startup. The
    /// constructor is not run: it wants a database context, while CanResolve
    /// reads constants only.
    /// </summary>
    private static bool Answers(Type generatorType, EventType eventType) =>
        ((INotificationGenerator)RuntimeHelpers.GetUninitializedObject(generatorType)).CanResolve(eventType);
}
