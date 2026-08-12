using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// One event produces one notification per recipient.
/// </summary>
/// <remarks>
/// Most generators leave the outgoing event type alone and the processor stamps
/// the event that triggered them. A few overwrite it, which is how a subscription
/// notification says "new game from a subscribed author" while answering the
/// creation of a game. That rename is a claim about the recipient, not about the
/// event: it only holds for someone who could not see the entity before.
///
/// The activation generators broke that. They renamed the event and, in the same
/// notification, addressed the subscribers of the entity's status changes — the
/// people the status generator answers on the very same event. Every activation
/// reached those readers twice, and the second copy called a game they were
/// already following a new one. Nothing failed and no count was off; the two
/// notifications simply disagreed about what had happened.
///
/// Asserted against the sources, because the audience is a constant inside a
/// method body and neither the compiler nor the IL shows that two generators
/// answering one event address one person.
/// </remarks>
public class NotificationAudienceShould
{
    private const string NotifiersDirectory =
        "src/DM.Workers.NotificationDispatcher/Notifiers";

    /// <summary>Assignment of an outgoing event type inside a CreateNotification.</summary>
    private const string RenamesTheEvent = "EventType = EventType.";

    /// <summary>The subscription flag the status generators are driven by.</summary>
    private const string StatusSubscribers = "SubscriptionSettings.StatusChanges";

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string[] RenamingGenerators => Directory
        .GetFiles(Path.Combine(RepositoryRoot, NotifiersDirectory), "*.cs", SearchOption.AllDirectories)
        .Where(file => File.ReadAllText(file).Contains(RenamesTheEvent, StringComparison.Ordinal))
        .ToArray();

    /// <summary>
    /// A rule that matches nothing passes. The subscription generators are the
    /// reason the rename exists, so finding none means the search string went
    /// stale rather than that the tree is clean.
    /// </summary>
    [Fact]
    public void FindTheGeneratorsThatRenameTheirEvent() =>
        RenamingGenerators.Should().HaveCountGreaterOrEqualTo(4);

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
