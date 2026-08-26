using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Forum.Boards;
using DM.Web.API.Features.Forum.Topics;
using AwesomeAssertions;
using Xunit;
using DomainBoard = DM.Domain.Forum.Features.Boards.Board;
using DomainTopic = DM.Domain.Forum.Features.Topics.Topic;

namespace DM.Web.API.Tests.Features.Forum;

/// <summary>
/// PATCH carries "not sent" as null and the write model must keep saying it:
/// the domain updates only the fields that arrived, so a mapper that defaults
/// an omitted one re-introduces the bug where pinning a topic blanked its
/// title.
/// </summary>
public class TopicMapperShould : UnitTestBase
{
    private TopicMapper CreateMapper()
    {
        var userMapper = new UserMapper(Mock<IImgproxyUrlBuilder>());
        return new TopicMapper(userMapper, new BoardMapper(userMapper));
    }

    /// <summary>
    /// The topic description is a BbText the client edits: getTopicForUpdate
    /// sends X-Dm-Audience: author_edit, and the converter honors that only
    /// when the envelope names the author. A topic without the envelope
    /// degrades the author's editor fetch to Display - and saving what the
    /// editor got back silently unwraps an author-moderator's [mod] markup.
    /// </summary>
    [Fact]
    public void WrapTheDescriptionInAnAuthorAwareEnvelope()
    {
        var authorId = Guid.Parse("00000000-0000-0000-0000-00000000000a");
        var topic = new DomainTopic
        {
            Id = Guid.NewGuid(),
            Title = "Заголовок",
            Text = "Текст [mod]служебное[/mod]",
            Author = new GeneralUser { UserId = authorId, Username = "author" },
            Board = new DomainBoard()
        };

        var result = CreateMapper().ToTopic(topic);

        var context = result.Description.Context;
        context.Should().NotBeNull("the converter needs the envelope to authorize AuthorEdit");
        context!.Surface.Should().Be(BbSurface.Comment);
        context.PostAuthorUserId.Should().Be(authorId);
    }

    [Fact]
    public void KeepAnUnsentUpdateFieldNull()
    {
        var update = CreateMapper().ToUpdateTopic(new UpdateTopicRequest
        {
            IsAttached = true
        });

        update.IsAttached.Should().BeTrue();
        update.Title.Should().BeNull();
        update.Text.Should().BeNull();
        update.BoardTitle.Should().BeNull("an absent board means \"do not move\"");
        update.IsClosed.Should().BeNull();
    }

    /// <summary>
    /// The domain resolves a board by alias, title or id and compares the
    /// value with the current board title to decide whether the edit is a
    /// move - so the free-form field must arrive as BoardTitle verbatim.
    /// </summary>
    [Fact]
    public void CarryTheBoardAsATitle()
    {
        var update = CreateMapper().ToUpdateTopic(new UpdateTopicRequest
        {
            Board = "Общий форум",
            Description = "Новый текст"
        });

        update.BoardTitle.Should().Be("Общий форум");
        update.Text.Should().Be("Новый текст");
    }
}
