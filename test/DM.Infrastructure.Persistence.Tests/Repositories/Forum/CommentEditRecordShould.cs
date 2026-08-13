using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Core.Abstractions;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.Repositories.Blog;
using DM.Infrastructure.Persistence.Repositories.Forum;
using DM.Infrastructure.Persistence.Repositories.Game;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Forum;

/// <summary>
/// Editing a comment leaves a trace, in all four discussions.
/// </summary>
/// <remarks>
/// A comment row keeps no modification stamp of its own: CommentMappingProfile
/// derives ModifiedUtc from the newest entry of the edit history, and the client
/// draws its "edited" mark from that field. All four repositories saved the new
/// text and wrote no entry, each under the same comment saying tracking was
/// handled by the history — so ModifiedUtc was null for every comment ever
/// edited, and an edit was invisible to every reader. The forum's topic history
/// is the same shape and has always been written.
///
/// All four branches are asserted here rather than one: a rule that holds in four
/// places and is asserted in one is asserted in one, which is how this went
/// unnoticed in the first place.
/// </remarks>
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
        var context = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

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
        var factory = new Mock<IGuidFactory>();
        factory.Setup(f => f.Create()).Returns(EditId);
        return factory.Object;
    }

    /// <summary>
    /// The update returns a projected comment, which needs a mapper the in-memory
    /// provider cannot serve here. The write itself has already happened by then,
    /// so the projection is allowed to throw and the trace is read from the tracker.
    /// </summary>
    private static async Task WriteAsync(Func<Task> update)
    {
        try
        {
            await update();
        }
        catch (NullReferenceException)
        {
            // The mapper is null: the projection, not the write.
        }
    }

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
            "topic" => new TopicCommentRepository(context, null!, guids, null!)
                .Update(new UpdateTopicCommentEntity
                {
                    CommentId = CommentId,
                    Text = "Исправленная реплика",
                    LastUpdateUtc = Edited,
                    EditorUserId = EditorId,
                }),
            "blog" => new BlogCommentRepository(context, null!, null!, guids)
                .Update(new UpdateBlogCommentEntity
                {
                    CommentId = CommentId,
                    Text = "Исправленная реплика",
                    LastUpdateUtc = Edited,
                    EditorUserId = EditorId,
                }),
            "publication" => new PublicationCommentRepository(context, null!, null!, guids)
                .Update(new UpdatePublicationCommentEntity
                {
                    CommentId = CommentId,
                    Text = "Исправленная реплика",
                    LastUpdateUtc = Edited,
                    EditorUserId = EditorId,
                }),
            _ => new GameCommentRepository(context, null!, guids)
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

    /// <summary>
    /// A caller that does not say who is editing gets no trace rather than a failed
    /// write: EditorUserId has a foreign key to Users, and Guid.Empty answers to
    /// nobody.
    /// </summary>
    [Fact]
    public async Task WriteNoTraceWhenTheEditorIsUnknown()
    {
        using var context = Context();

        await WriteAsync(() => new BlogCommentRepository(context, null!, null!, Guids())
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
