using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
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
/// The list in the application is the third reader of the same words, and the
/// table moved into the domain so that the API could read it. The rule looks
/// wherever a notification is put into words: the dispatcher that sends it, the
/// API that lists it, and the domain both of them read the shared table from.
/// </remarks>
public class NotificationWordingShould
{
    private const string EventTypeName = "DM.Domain.Core.Enums.EventType";

    private const BindingFlags DeclaredStatic =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>The assemblies that put a notification into words.</summary>
    private static readonly Assembly[] Readers =
    {
        typeof(DM.Workers.NotificationDispatcher.Startup).Assembly,
        typeof(DM.Web.API.Startup).Assembly,
        typeof(NotificationWording).Assembly
    };

    [Fact]
    public void KeepOneEventTitleTable()
    {
        var tables = Readers
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(t => t.GetFields(DeclaredStatic))
            .Where(f => IsEventTitleTable(f.FieldType))
            .ToList();

        tables.Should().ContainSingle(
            "the words a notification is phrased in must have one owner, not one per channel");
        tables[0].DeclaringType.Should().Be(typeof(NotificationWording));
    }

    [Fact]
    public void KeepOneMetadataFormatter()
    {
        var formatters = Readers
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(t => t.GetMethods(DeclaredStatic))
            .Where(m => m.Name is "FormatPropertyName" or "FormatPropertyValue" or "IsHiddenFromText")
            .ToList();

        formatters.Should().HaveCountGreaterThanOrEqualTo(3,
            "a rule that matches nothing passes: there is a formatter for the key, " +
            "one for the value, and the list of keys no channel prints");
        formatters.Select(m => m.DeclaringType).Should().OnlyContain(
            type => type == typeof(NotificationWording),
            "a channel that formats metadata its own way drifts from the others silently");
    }

    private static bool IsEventTitleTable(Type type) =>
        type.IsGenericType
        && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)
        && type.GetGenericArguments()[0].FullName == EventTypeName
        && type.GetGenericArguments()[1] == typeof(string);
}
