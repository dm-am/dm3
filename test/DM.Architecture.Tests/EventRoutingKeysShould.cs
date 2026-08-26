using System;
using System.Linq;
using System.Reflection;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every event type is one event type, on both sides of the wire.
/// </summary>
/// <remarks>
/// The enum is the whole routing table of this system: the key of a member is
/// what a queue is bound by, and the number of it is what travels in the message
/// body — the client serialises an enum as its number, so the number is the
/// identity a consumer reads.
///
/// Nothing fails when a member breaks either rule. A member with no key is
/// published under an empty routing key, matches no binding and is dropped at the
/// exchange; two members sharing a number arrive at the consumer as the same
/// event, and the generators of one of them start firing for the other. In both
/// cases the site keeps running, the queues stay short, and the symptom is a
/// reader who was told about the wrong thing or about nothing.
///
/// Walked over the fields rather than over Enum.GetValues: values and names come
/// back as parallel arrays there, so a duplicated number is silently one entry
/// and the check about numbers would pass by construction.
/// </remarks>
public class EventRoutingKeysShould
{
    /// <summary>
    /// The one member with no key, and the one that must not have one: it is the
    /// default of the type, so it is what an unset field deserialises to rather
    /// than an event anybody publishes.
    /// </summary>
    private const string Unrouted = nameof(EventType.Unknown);

    private static FieldInfo[] Members => typeof(EventType)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(member => member.Name != Unrouted)
        .ToArray();

    [Fact]
    public void CarryExactlyOneRoutingKeyEach()
    {
        Members.Should().NotBeEmpty(
            "the enum declares the events of this system, and a walk that finds none of them " +
            "passes whatever they say");

        Members
            .Where(field => field.GetCustomAttributes<EventRoutingKeyAttribute>(false).Count() != 1)
            .Select(field => field.Name)
            .Should().BeEmpty(
                "a member with no key is published under an empty one, matches no binding and " +
                "is dropped at the exchange without a trace; a member with two is read by " +
                "whichever the reflection happens to return first");
    }

    [Fact]
    public void NameOneEventEach()
    {
        Keyed
            .GroupBy(Key, StringComparer.Ordinal)
            .Where(shared => shared.Count() > 1)
            .Select(shared => $"{shared.Key}: {string.Join(", ", shared.Select(field => field.Name))}")
            .Should().BeEmpty(
                "two members under one key bind the same queue twice and hand the generators " +
                "of one event the other one's entity");
    }

    [Fact]
    public void BeTellableApartOnTheWire()
    {
        Members
            .GroupBy(field => field.GetRawConstantValue())
            .Where(shared => shared.Count() > 1)
            .Select(shared => $"{shared.Key}: {string.Join(", ", shared.Select(field => field.Name))}")
            .Should().BeEmpty(
                "the number is what travels in the message, so members sharing one arrive at " +
                "the consumer as the same event whatever they were published as");
    }

    /// <summary>
    /// A key is a name and not a pattern.
    /// </summary>
    /// <remarks>
    /// The queue of the dispatcher is bound by the keys of the events some
    /// generator handles. A wildcard in one of them widens that binding to
    /// everything matching it, so the queue starts collecting events no generator
    /// declared - and every one of them is taken, found unhandled, and produces
    /// nothing, which is indistinguishable from the events simply not arriving.
    /// </remarks>
    [Fact]
    public void BindNothingButThemselves()
    {
        Keyed
            .Where(field => Key(field).Contains('*') || Key(field).Contains('#'))
            .Select(field => $"{field.Name}: {Key(field)}")
            .Should().BeEmpty(
                "a wildcard here is a binding, not a name: the queue of the dispatcher would " +
                "start taking events nothing in it handles");
    }

    [Fact]
    public void LeaveTheDefaultOfTheTypeUnpublishable() =>
        typeof(EventType).GetField(Unrouted)!
            .GetCustomAttributes<EventRoutingKeyAttribute>(false).Should().BeEmpty(
                "this is what an absent field deserialises to, so a key on it would turn every " +
                "message that forgot to say what it was into a routable event");

    /// <summary>
    /// The key of a member, or nothing where it has none. Empty rather than a
    /// throw so that a member with no attribute reddens the fact about attributes
    /// and only that one - a walk that dies here reports every rule of this class
    /// as broken and names none of them.
    /// </summary>
    private static string Key(FieldInfo field) =>
        field.GetCustomAttributes<EventRoutingKeyAttribute>(false).FirstOrDefault()?.RoutingKey
        ?? string.Empty;

    private static FieldInfo[] Keyed => Members.Where(member => Key(member).Length > 0).ToArray();
}
