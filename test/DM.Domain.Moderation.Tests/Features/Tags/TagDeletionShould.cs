using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tags;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tags;

/// <summary>
/// A tag is deleted only while no game wears it, by the count the tag is read with.
/// </summary>
public class TagDeletionShould : UnitTestBase
{
    private readonly ITagManagementRepository repository;
    private readonly TagManagementService service;
    private readonly Guid tagId = Guid.NewGuid();

    public TagDeletionShould()
    {
        repository = Mock<ITagManagementRepository>();
        service = new TagManagementService(
            Mock<IIntentionManager>(),
            repository,
            Mock<IValidator<CreateTagGroup>>(),
            Mock<IValidator<UpdateTagGroup>>(),
            Mock<IValidator<CreateTag>>(),
            Mock<IValidator<UpdateTag>>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RefuseToDeleteATagGamesWear(int gamesCount)
    {
        repository.GetTag(tagId, Arg.Any<CancellationToken>())
            .Returns(new Tag { Id = tagId, Title = "Фэнтези", GamesCount = gamesCount });

        var act = () => service.DeleteTag(tagId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict)
            .WithMessage($"*({gamesCount})*");
        await repository.DidNotReceive().DeleteTag(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteATagNoGameWears()
    {
        repository.GetTag(tagId, Arg.Any<CancellationToken>())
            .Returns(new Tag { Id = tagId, Title = "Фэнтези", GamesCount = 0 });

        await service.DeleteTag(tagId);

        await repository.Received(1).DeleteTag(tagId, Arg.Any<CancellationToken>());
    }
}
