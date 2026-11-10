using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Core.Abstractions;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Game.Features.Comments;
using DM.Infrastructure.Persistence.Repositories.Blog;
using DM.Infrastructure.Persistence.Repositories.Forum;
using DM.Infrastructure.Persistence.Repositories.Game;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Forum;

// CommentProjections derives ModifiedUtc from the newest history entry, and the client draws
// "edited" from it.
public class CommentEditRecordShould
{
    private static readonly DateTimeOffset Created = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Edited = new(2026, 4, 1, 12, 30, 0, TimeSpan.Zero);

    private static readonly Guid EntityId = Guid.Parse("3a5d9e10-0000-4000-8000-000000000001");
    private static readonly Guid AuthorId = Guid.Parse("3a5d9e10-0000-4000-8000-000000000002");
    private static readonly Guid EditorId = Guid.Parse("3a5d9e10-0000-4000-8000-000000000003");
    private static readonly Guid CommentId = Guid.Parse("3a5d9e10-0000-4000-8000-000000000011");
    private static readonly Guid EditId = Guid.Parse("3a5d9e10-0000-4000-8000-000000000021");

    private static DmDbContext Context()
    {
        var context = InMemoryStore.Open();

        // The author row exists so the projection the update answers with can
        // join it - a comment's author is a required navigation.
        context.Users.Add(new Entities.Account.User
        {
            UserId = AuthorId,
            Username = "author",
            Email = "author@test.local",
            Salt = "",
            PasswordHash = "",
            CreatedUtc = Created,
        });

        context.Comments.Add(new DbComment
        {
            CommentId = CommentId,
            EntityId = EntityId,
            AuthorId = AuthorId,
            Text = "Реплика",
            CreatedUtc = Created,
            IsRemoved = false,
        });
        context.SaveChanges();
        return context;
    }

    private static IGuidFactory Guids()
    {
        var factory = Substitute.For<IGuidFactory>();
        factory.Create().Returns(EditId);
        return factory;
    }

    private static Task WriteAsync(Func<Task> update) => update();

    [Theory]
    [InlineData("topic")]
    [InlineData("blog")]
    [InlineData("publication")]
    [InlineData("game")]
    public async Task RecordWhoEditedTheCommentAndWhen(string discussion)
    {
        using var context = Context();
        var guids = Guids();

        await WriteAsync(() => discussion switch
        {
            "topic" => new TopicCommentRepository(context, guids, null!)
                .Update(new UpdateTopicCommentEntity
                {
                    CommentId = CommentId,
                    Text = "Исправленная реплика",
                    LastUpdateUtc = Edited,
                    EditorUserId = EditorId,
                }),
            "blog" => new BlogCommentRepository(context, null!, guids)
                .Update(new UpdateBlogCommentEntity
                {
                    CommentId = CommentId,
                    Text = "Исправленная реплика",
                    LastUpdateUtc = Edited,
                    EditorUserId = EditorId,
                }),
            "publication" => new PublicationCommentRepository(context, null!, guids)
                .Update(new UpdatePublicationCommentEntity
                {
                    CommentId = CommentId,
                    Text = "Исправленная реплика",
                    LastUpdateUtc = Edited,
                    EditorUserId = EditorId,
                }),
            _ => new GameCommentRepository(context, guids)
                .Update(new UpdateGameCommentEntity
                {
                    CommentId = CommentId,
                    Text = "Исправленная реплика",
                    ModifiedUtc = Edited,
                    EditorUserId = EditorId,
                }),
        });

        var edit = context.CommentEdits.Single();
        edit.CommentId.Should().Be(CommentId);
        edit.EditorUserId.Should().Be(EditorId);
        edit.EditedUtc.Should().Be(Edited);
        context.Comments.Single().Text.Should().Be("Исправленная реплика");
    }

    // A caller that names no editor gets no trace rather than a failed write: Guid.Empty answers to
    // nobody.
    [Fact]
    public async Task WriteNoTraceWhenTheEditorIsUnknown()
    {
        using var context = Context();

        await WriteAsync(() => new BlogCommentRepository(context, null!, Guids())
            .Update(new UpdateBlogCommentEntity
            {
                CommentId = CommentId,
                Text = "Исправленная реплика",
                LastUpdateUtc = Edited,
            }));

        context.CommentEdits.Should().BeEmpty();
        context.Comments.Single().Text.Should().Be("Исправленная реплика");
    }
}
