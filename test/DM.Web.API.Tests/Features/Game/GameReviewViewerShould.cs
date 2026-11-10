using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.GameReviews;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Reviews;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.Game;

// The pencil and the trash of a review are the domain's answers, so the page counts neither.
public class GameReviewViewerShould : UnitTestBase
{
    private readonly IGameReviewService reviewService = Mock<IGameReviewService>();
    private readonly GameReviewApiService service;

    public GameReviewViewerShould()
    {
        service = new GameReviewApiService(
            reviewService, Mock<IUserLookupService>(), new ReviewMapper(new UserMapper()));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CarryTheDomainAnswersOnEveryReviewOfTheList(bool mayEdit, bool mayDelete)
    {
        var gameId = Guid.NewGuid();
        var review = new GameReview { Id = Guid.NewGuid(), GameId = gameId, Text = "Отлично" };
        reviewService.GetListAsync(gameId, Arg.Any<PagingQuery>()).Returns(([review], PagingResult.Empty(20)));
        reviewService.MayEdit(review).Returns(mayEdit);
        reviewService.MayDelete(review).Returns(mayDelete);

        var listed = await service.GetList(gameId, new PagingQuery());

        listed.Resources.Should().ContainSingle().Which.Viewer.Should().BeEquivalentTo(
            new GameReviewViewer { CanEdit = mayEdit, CanDelete = mayDelete });
    }

    // An edit answers with the review in its row, so the row keeps the answers too.
    [Fact]
    public async Task CarryTheDomainAnswersOnAnEditedReview()
    {
        var review = new GameReview { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), Text = "Отлично" };
        reviewService.UpdateAsync(Arg.Any<UpdateGameReview>()).Returns(review);
        reviewService.MayEdit(review).Returns(true);
        reviewService.MayDelete(review).Returns(true);

        var edited = await service.Update(review.Id, new UpdateGameReviewRequest { Text = "Отлично" });

        edited.Resource.Viewer.Should().BeEquivalentTo(new GameReviewViewer { CanEdit = true, CanDelete = true });
    }
}
