using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A personal blacklist reaches notifications, and it reaches them once.
/// </summary>
/// <remarks>
/// Blocking somebody used to stop their comments in a listing and their direct
/// messages, and nothing else: the site kept writing about their likes, their
/// comments and their publications, in the notification list, by mail and
/// through the bots. The block was the loudest statement a reader can make
/// about another person, and the one surface they cannot scroll past ignored it.
///
/// The reason it could not be fixed where it was noticed is why this rule
/// exists. The notification DTO carried the actor only as a display name inside
/// an anonymous metadata object, so there was nothing to filter on; and the
/// forty-one generators that build notifications are the wrong place for the
/// rule anyway, because a rule written in forty-one places is a rule the
/// forty-second forgets. The actor is now a field, and the filter is one call in
/// the domain service every notification passes through.
///
/// Two halves are asserted, and both are the way this can quietly come back.
/// The service must still consult the blacklist while creating. And the
/// dispatcher must build every channel out of what the service answered: the
/// audience it was asked for is still in a local variable there, and reading it
/// for mail delivers exactly the notification the filter refused to store.
/// </remarks>
public class NotificationRecipientsShould
{
    private const string Service =
        "src/DM.Domain.Personal/Features/Notifications/NotificationService.cs";

    private const string Dispatcher =
        "src/DM.Workers.NotificationDispatcher/Implementation/NotificationProcessor.cs";

    [Fact]
    public void BeFilteredByThePersonalBlacklistWhileTheyAreCreated()
    {
        var service = Read(Service);

        service.Should().Contain("IUserBlacklistChecker",
            "the filter needs the blacklist, and the service is where it is applied");

        service.Should().Contain("GetOwnersBlockingAsync",
            "the read is by actor across the whole audience: asking per recipient " +
            "turns one notification to a hundred subscribers into a hundred statements");

        // The call, not the declaration. A private method nobody invokes is the
        // same as no filter and reads like one that works, and matching the bare
        // name matches the method's own signature — which is how the first
        // version of this rule passed with the call taken out.
        service.Should().Contain("await ExcludeBlockedRecipients(",
            "the filter has to be awaited on the creation path, not merely declared");

        // Whether the result of that call is what the notifications are built
        // from is a question about behaviour, and NotificationServiceShould
        // answers it: a recipient who blocked the actor is not addressed.
    }

    [Fact]
    public void ReachEveryChannelThroughTheFilteredAudience()
    {
        // Comments stripped, and only the lines after the creation call are read:
        // the call passes the requested list by definition, and a comment saying
        // not to read it afterwards is the opposite of the defect.
        var afterCreate = Read(Dispatcher)
            .Split('\n')
            .Select(line => Regex.Replace(line, @"//.*$", string.Empty))
            .SkipWhile(line => !line.Contains("CreateAsync", StringComparison.Ordinal))
            .Skip(1);

        afterCreate
            .Where(line => Regex.IsMatch(line, @"\bnotificationsToCreate\b"))
            .Should().BeEmpty(
                "past the creation call that list is the audience nobody filtered. Mail " +
                "and the bots used to be built from it, so a recipient the filter removed " +
                "from the stored row still got the letter");
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !(Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                     Directory.Exists(Path.Combine(directory.FullName, "test"))))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }
}
