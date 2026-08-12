using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every notification channel takes its wording from one table.
/// </summary>
/// <remarks>
/// The email sender and the bot sender each owned a private copy of the same
/// forty-eight event titles and of the same two metadata formatters. Nothing
/// linked the copies, so they drifted: the email formatter knew the IsReminder
/// metadata key and the bot formatter did not, and only a diff of the two files
/// could show it. Duplication of this shape is invisible to the compiler and to
/// a reviewer reading one file at a time, so it needs a rule rather than a habit.
///
/// The shared type is matched by name because it is internal to the worker and
/// this suite is not on its InternalsVisibleTo list.
/// </remarks>
public class NotificationWordingShould
{
    private const string SharedType = "DM.Workers.NotificationDispatcher.Dispatching.NotificationText";
    private const string EventTypeName = "DM.Domain.Core.Enums.EventType";

    private const BindingFlags DeclaredStatic =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly Assembly Dispatcher =
        typeof(DM.Workers.NotificationDispatcher.Startup).Assembly;

    [Fact]
    public void KeepOneEventTitleTable()
    {
        var tables = Dispatcher.GetTypes()
            .SelectMany(t => t.GetFields(DeclaredStatic))
            .Where(f => IsEventTitleTable(f.FieldType))
            .ToList();

        tables.Should().ContainSingle(
            "the words a notification is phrased in must have one owner, not one per channel");
        tables[0].DeclaringType!.FullName.Should().Be(SharedType);
    }

    [Fact]
    public void KeepOneMetadataFormatter()
    {
        var formatters = Dispatcher.GetTypes()
            .SelectMany(t => t.GetMethods(DeclaredStatic))
            .Where(m => m.Name is "FormatPropertyName" or "FormatPropertyValue" or "IsHiddenFromText")
            .ToList();

        formatters.Should().HaveCountGreaterOrEqualTo(3,
            "a rule that matches nothing passes: there is a formatter for the key, " +
            "one for the value, and the list of keys no channel prints");
        formatters.Select(m => m.DeclaringType!.FullName).Should().OnlyContain(
            name => name == SharedType,
            "a channel that formats metadata its own way drifts from the others silently");
    }

    private static bool IsEventTitleTable(Type type) =>
        type.IsGenericType
        && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)
        && type.GetGenericArguments()[0].FullName == EventTypeName
        && type.GetGenericArguments()[1] == typeof(string);
}
