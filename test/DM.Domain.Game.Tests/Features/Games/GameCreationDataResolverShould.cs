using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

/// <summary>
/// The one place a submitted set of tags is read, and therefore the one place
/// the per-group limits are applied.
/// </summary>
/// <remarks>
/// Creation and the settings page both come through here, so a case proved on
/// this method is proved for both of them.
/// </remarks>
public class GameCreationDataResolverShould : UnitTestBase
{
    private static readonly Guid FirstSystemTag = Guid.NewGuid();
    private static readonly Guid SecondSystemTag = Guid.NewGuid();
    private static readonly Guid ThirdSystemTag = Guid.NewGuid();

    private readonly IGameRepository _repository;
    private readonly GameCreationDataResolver _resolver;

    public GameCreationDataResolverShould()
    {
        _repository = Mock<IGameRepository>();
        _repository.GetTags(Arg.Any<CancellationToken>()).Returns(Catalogue);

        _resolver = new GameCreationDataResolver(
            _repository,
            Mock<IAttributeSchemaRepository>(),
            Mock<IIntentionManager>());
    }

    [Fact]
    public async Task TranslateASetWithinTheLimits()
    {
        var resolved = await _resolver.ResolveTagIds([1, 2]);

        resolved.Should().BeEquivalentTo(new[] { FirstSystemTag, SecondSystemTag });
    }

    [Fact]
    public async Task RefuseASetOverAGroupLimit()
    {
        var resolve = async () => await _resolver.ResolveTagIds([1, 2, 3]);

        (await resolve.Should().ThrowAsync<HttpBadRequestException>(
                "the group takes two tags and three were submitted"))
            .Which.ValidationErrors["tags"].Should().Contain("Система");
    }

    /// <summary>
    /// A field that was not sent is not a set to measure.
    /// </summary>
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
        var resolved = await _resolver.ResolveTagIds(null);

        resolved.Should().BeEmpty();
        await _repository.DidNotReceive().GetTags(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An identifier the catalogue does not know is dropped, and dropped before
    /// it is counted: a limit must not be reached by tags that do not exist.
    /// </summary>
    [Fact]
    public async Task CountOnlyTagsTheCatalogueKnows()
    {
        var resolved = await _resolver.ResolveTagIds([1, 2, 404]);

        resolved.Should().BeEquivalentTo(new[] { FirstSystemTag, SecondSystemTag });
    }

    /// <summary>
    /// The same tag named twice is one tag, and counts once.
    /// </summary>
    [Fact]
    public async Task CountARepeatedTagOnce()
    {
        var resolve = async () => await _resolver.ResolveTagIds([1, 1, 2, 2]);

        await resolve.Should().NotThrowAsync();
    }

    /// <summary>
    /// A catalogue of one limited group and one unlimited one. Written out here
    /// rather than read from the seed: the rule under test is "whatever the group
    /// says", not the numbers the site happens to ship with.
    /// </summary>
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
