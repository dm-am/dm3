using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Asserted against the sources, as NotificationAudienceShould is: the bit is an argument of the
/// audience query inside a method body. Each event of the words stands for one generator.
/// </remarks>
public class UserSubscriptionCategoriesShould
{
    private const string NotifiersDirectory =
        "src/DM.Workers.NotificationDispatcher/Notifiers";

    private static string[] SendersUnder(SubscriptionSettings category)
    {
        var asked = new Regex($@"\bSubscriptionSettings\.{category}\b");
        return Directory
            .GetFiles(Path.Combine(RepositoryLayout.Root, NotifiersDirectory), "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadLines(file)
                .Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal))
                .Any(asked.IsMatch))
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    [Fact]
    public void ListEveryAuthorCategoryOnce() =>
        UserSubscriptionCategories.All.Select(category => category.Category).Should().Equal(
            new[]
            {
                SubscriptionSettings.AuthorGameEvents,
                SubscriptionSettings.AuthorBlogEvents,
                SubscriptionSettings.AuthorTopicEvents,
            },
            "the panel offers each category of a subscription to a user, and the bits outside them are channels");

    [Theory]
    [InlineData(SubscriptionSettings.AuthorGameEvents)]
    [InlineData(SubscriptionSettings.AuthorBlogEvents)]
    [InlineData(SubscriptionSettings.AuthorTopicEvents)]
    public void NameAnEventForEveryGeneratorThatSendsUnderTheBit(SubscriptionSettings category)
    {
        var senders = SendersUnder(category);
        var events = UserSubscriptionCategories.All.Single(entry => entry.Category == category).Events
            .Split(", ");

        senders.Should().NotBeEmpty("a category nobody sends under is a box that does nothing");
        events.Should().HaveSameCount(senders,
            $"the words of {category} promise one event for each of {string.Join(", ", senders)}, " +
            "and a generator added under the bit without a word in the panel sends what the reader " +
            "was not told to expect");
    }
}
