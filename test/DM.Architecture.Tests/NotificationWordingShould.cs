using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

// Read wherever a notification is put into words: the dispatcher, the API and the domain table both
// read.
public class NotificationWordingShould
{
    private const string EventTypeName = "DM.Domain.Core.Enums.EventType";

    private const BindingFlags DeclaredStatic =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

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
