using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Reviews;
using AwesomeAssertions;
using Xunit;
using DomainGameReview = DM.Domain.Game.Features.GameReviews.GameReview;
using DomainPostReview = DM.Domain.Game.Features.PostReviews.PostReview;

namespace DM.Web.API.Tests.Features.Game;

/// <summary>
/// Both review texts are edited in place: the editor fetches them with
/// X-Dm-Audience: author_edit, and the converter honors that only when the
/// envelope names the author. A review without the envelope hands its author
/// the display rendering, and saving it writes the typography back as text.
/// </summary>
public class ReviewMapperShould : UnitTestBase
{
    private static readonly Guid AuthorId = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    private ReviewMapper CreateMapper() => new(new UserMapper(Mock<IImgproxyUrlBuilder>()));

    [Fact]
    public void WrapTheGameReviewTextInAnAuthorAwareEnvelope()
    {
        var review = new DomainGameReview
        {
            Id = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            Author = new GeneralUser { UserId = AuthorId, Username = "author" },
            Text = "Раз -- два"
        };

        var context = CreateMapper().ToGameReview(review).Text!.Context;

        context.Should().NotBeNull("the converter needs the envelope to authorize AuthorEdit");
        context!.Surface.Should().Be(BbSurface.Comment);
        context.PostAuthorUserId.Should().Be(AuthorId);
    }

    [Fact]
    public void WrapThePostReviewTextInAnAuthorAwareEnvelope()
    {
        var review = new DomainPostReview
        {
            Id = Guid.NewGuid(),
            PostId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            Author = new GeneralUser { UserId = AuthorId, Username = "author" },
            PostAuthor = new GeneralUser { UserId = Guid.NewGuid(), Username = "poster" },
            Text = "Раз -- два"
        };

        var context = CreateMapper().ToPostReview(review).Text!.Context;

        context.Should().NotBeNull("the converter needs the envelope to authorize AuthorEdit");
        context!.Surface.Should().Be(BbSurface.Comment);
        context.PostAuthorUserId.Should().Be(AuthorId);
    }
}
