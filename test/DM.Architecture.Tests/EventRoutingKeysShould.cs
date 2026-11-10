using System;
using System.Linq;
using System.Reflection;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// The key binds the queue and the number travels in the body. Walked over the fields: Enum.GetValues
/// folds a duplicated number into one entry.
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

    // A wildcard widens the dispatcher's binding to events no generator handles.
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
    /// The key of a member, or empty where it has none, so only the fact about attributes fails.
    /// </summary>
    private static string Key(FieldInfo field) =>
        field.GetCustomAttributes<EventRoutingKeyAttribute>(false).FirstOrDefault()?.RoutingKey
        ?? string.Empty;

    private static FieldInfo[] Keyed => Members.Where(member => Key(member).Length > 0).ToArray();
}
