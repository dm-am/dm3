using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// One event produces one notification per recipient.
/// </summary>
/// <remarks>
/// A generator that renames the event claims the recipient could not see the entity before, so it
/// must not address the status subscribers. Asserted against the sources: the audience is a constant
/// inside a method body.
/// </remarks>
public class NotificationAudienceShould
{
    private const string NotifiersDirectory =
        "src/DM.Workers.NotificationDispatcher/Notifiers";

    /// <summary>Assignment of an outgoing event type inside a CreateNotification.</summary>
    private const string RenamesTheEvent = "EventType = EventType.";

    /// <summary>The subscription flag the status generators are driven by.</summary>
    private const string StatusSubscribers = "SubscriptionSettings.StatusChanges";


    private static string[] RenamingGenerators => Directory
        .GetFiles(Path.Combine(RepositoryLayout.Root, NotifiersDirectory), "*.cs", SearchOption.AllDirectories)
        .Where(file => File.ReadAllText(file).Contains(RenamesTheEvent, StringComparison.Ordinal))
        .ToArray();

    /// <summary>
    /// A rule that matches nothing passes, so the subscription generators are counted first.
    /// </summary>
    [Fact]
    public void FindTheGeneratorsThatRenameTheirEvent() =>
        RenamingGenerators.Should().NotBeEmpty();

    [Fact]
    public void LeaveStatusSubscribersToTheStatusGenerator()
    {
        var both = RenamingGenerators
            .Where(file => File.ReadAllText(file).Contains(StatusSubscribers, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();

        both.Should().BeEmpty(
            "a generator that renames its event must not address the people the status " +
            "generator already answers on that same event, or one activation arrives twice " +
            "under two different names");
    }
}
