using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

// Creation and the settings page both come through here, so a case proved on this method is proved
// for both of them.
public class GameTagResolverShould : UnitTestBase
{
    private static readonly Guid FirstSystemTag = Guid.NewGuid();
    private static readonly Guid SecondSystemTag = Guid.NewGuid();
    private static readonly Guid ThirdSystemTag = Guid.NewGuid();

    private readonly IGameRepository repository;
    private readonly GameTagResolver resolver;

    public GameTagResolverShould()
    {
        repository = Mock<IGameRepository>();
        repository.GetTags(Arg.Any<CancellationToken>()).Returns(Catalogue);

        resolver = new GameTagResolver(repository);
    }

    [Fact]
    public async Task TranslateASetWithinTheLimits()
    {
        var resolved = await resolver.ResolveTagIds([1, 2]);

        resolved.Should().BeEquivalentTo(new[] { FirstSystemTag, SecondSystemTag });
    }

    [Fact]
    public async Task RefuseASetOverAGroupLimit()
    {
        var resolve = async () => await resolver.ResolveTagIds([1, 2, 3]);

        (await resolve.Should().ThrowAsync<HttpBadRequestException>(
                "the group takes two tags and three were submitted"))
            .Which.ValidationErrors["tags"].Should().Contain("Система");
    }

    /// <remarks>
    /// The update endpoint takes tags as optional, where absence means "leave
    /// them alone". Games imported from DM2 may carry more tags of a group than
    /// the group now allows, and saving anything else about such a game must not
    /// fail on tags nobody touched. Asserting the catalogue is never even read
    /// rather than only that nothing was thrown: the point is that no check runs,
    /// not that this particular set happened to pass one.
    /// </remarks>
    [Fact]
    public async Task NotMeasureTagsThatWereNotSubmitted()
    {
        var resolved = await resolver.ResolveTagIds(null);

        resolved.Should().BeEmpty();
        await repository.DidNotReceive().GetTags(Arg.Any<CancellationToken>());
    }

    // An identifier the catalogue does not know is dropped, and dropped before it is counted: a
    // limit must not be reached by tags that do not exist.
    [Fact]
    public async Task CountOnlyTagsTheCatalogueKnows()
    {
        var resolved = await resolver.ResolveTagIds([1, 2, 404]);

        resolved.Should().BeEquivalentTo(new[] { FirstSystemTag, SecondSystemTag });
    }

    [Fact]
    public async Task CountARepeatedTagOnce()
    {
        var resolve = async () => await resolver.ResolveTagIds([1, 1, 2, 2]);

        await resolve.Should().NotThrowAsync();
    }

    // Written out here rather than read from the seed: the rule under test is "whatever the group
    // says", not the numbers the site happens to ship with.
    private static IEnumerable<GameTag> Catalogue =>
    [
        SystemTag(FirstSystemTag, 1),
        SystemTag(SecondSystemTag, 2),
        SystemTag(ThirdSystemTag, 3),
        new GameTag
        {
            Id = Guid.NewGuid(),
            ShortId = 10,
            Title = "Без мата",
            GroupTitle = "Ограничения",
            GroupSortOrder = 1,
            GroupMaxTagsPerGame = null
        }
    ];

    private static GameTag SystemTag(Guid id, int shortId) => new()
    {
        Id = id,
        ShortId = shortId,
        Title = $"Система-{shortId}",
        GroupTitle = "Система",
        GroupSortOrder = 0,
        GroupMaxTagsPerGame = 2
    };
}
