using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

/// <summary>
/// A game may take only so many tags out of one group.
/// </summary>
/// <remarks>
/// The number is the group's own, served with the catalogue, so these cases
/// build groups rather than name the ones the site ships with: the rule is
/// "whatever the group says", and a test written against "Жанр is three" would
/// start failing the day moderation moves it.
/// </remarks>
public class TagGroupLimitShould
{
    [Fact]
    public void AllowAGroupFilledExactlyToItsLimit()
    {
        var selected = Group("Система", limit: 2, count: 2);

        var check = () => TagGroupLimit.ThrowIfExceeded(selected);

        check.Should().NotThrow("the limit is how many may be carried, not how many are too many");
    }

    [Fact]
    public void RefuseAGroupOverItsLimit()
    {
        var selected = Group("Система", limit: 2, count: 3);

        var check = () => TagGroupLimit.ThrowIfExceeded(selected);

        check.Should().Throw<HttpBadRequestException>()
            .Which.ValidationErrors["tags"].Should()
            .Contain("Система", "the refusal names the group the master has to fix")
            .And.Contain("2", "and the number it may not go past");
    }

    /// <summary>
    /// A group taking one tag is a switch on the form, and the refusal says so
    /// in words rather than as an arithmetic bound.
    /// </summary>
    [Fact]
    public void SayASingleTagInWordsWhenTheLimitIsOne()
    {
        var selected = Group("Темп", limit: 1, count: 2);

        var check = () => TagGroupLimit.ThrowIfExceeded(selected);

        check.Should().Throw<HttpBadRequestException>()
            .Which.ValidationErrors["tags"].Should().Be(
                "В группе \"Темп\" можно выбрать только один тег");
    }

    [Fact]
    public void AllowAnyNumberOfTagsFromAGroupThatSetsNoLimit()
    {
        var selected = Group("Ограничения", limit: null, count: 5);

        var check = () => TagGroupLimit.ThrowIfExceeded(selected);

        check.Should().NotThrow("an empty limit is the absence of one, not a limit of zero");
    }

    [Fact]
    public void MeasureEachGroupSeparately()
    {
        var selected = Group("Система", limit: 2, count: 2)
            .Concat(Group("Жанр", limit: 3, count: 3))
            .ToList();

        var check = () => TagGroupLimit.ThrowIfExceeded(selected);

        check.Should().NotThrow("tags of one group do not count against another");
    }

    /// <summary>
    /// One refusal, every group that is over, in the order the form draws them:
    /// the master fixes the whole set once instead of resubmitting to discover
    /// the next group.
    /// </summary>
    [Fact]
    public void NameEveryGroupOverItsLimit()
    {
        var selected = Group("Жанр", limit: 3, count: 4, groupSortOrder: 1)
            .Concat(Group("Система", limit: 2, count: 3, groupSortOrder: 0))
            .ToList();

        var check = () => TagGroupLimit.ThrowIfExceeded(selected);

        check.Should().Throw<HttpBadRequestException>()
            .Which.ValidationErrors["tags"].Should().Be(
                "В группе \"Система\" можно выбрать не больше 2 тегов. " +
                "В группе \"Жанр\" можно выбрать не больше 3 тегов");
    }

    [Fact]
    public void AllowAnEmptySet()
    {
        var check = () => TagGroupLimit.ThrowIfExceeded([]);

        check.Should().NotThrow("clearing every tag is a set within every limit");
    }

    /// <summary>Tags of one group, as the catalogue hands them over.</summary>
    private static List<GameTag> Group(string title, int? limit, int count, int groupSortOrder = 0) =>
        Enumerable.Range(0, count)
            .Select(i => new GameTag
            {
                ShortId = groupSortOrder * 100 + i,
                Title = $"{title}-{i}",
                GroupTitle = title,
                GroupSortOrder = groupSortOrder,
                GroupMaxTagsPerGame = limit,
                SortOrder = i
            })
            .ToList();
}
