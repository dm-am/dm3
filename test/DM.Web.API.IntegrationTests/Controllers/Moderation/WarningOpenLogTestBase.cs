using Microsoft.EntityFrameworkCore;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DM.Web.API.IntegrationTests.Helpers;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// What the tests of the open log share: the comments their warnings are given for, written straight
/// into the table and removed after.
/// </summary>
public abstract class WarningOpenLogTestBase : WarningControllerTestBase
{
    protected WarningOpenLogTestBase(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    protected async Task SeedComment(Guid commentId, Guid placeId, string text)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Comments.Add(new DbComment
        {
            CommentId = commentId,
            EntityId = placeId,
            AuthorId = TestConstants.SecondUserId,
            Text = text,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();
    }

    protected async Task SeedTopicComment(Guid commentId, string text)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Comments.Add(new DbComment
        {
            CommentId = commentId,
            EntityId = TestConstants.TestTopicId,
            AuthorId = TestConstants.SecondUserId,
            Text = text,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-3)
        });
        await db.SaveChangesAsync();
    }

    protected async Task UnseedComment(Guid commentId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Comments.IgnoreQueryFilters().Where(c => c.CommentId == commentId).ExecuteDeleteAsync();
    }
}
