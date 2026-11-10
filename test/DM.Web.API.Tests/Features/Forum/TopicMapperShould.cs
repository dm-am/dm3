using System;
using DM.Domain.Core.Dto;
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

// PATCH carries "not sent" as null and the write model keeps saying it: the domain updates only the
// fields that arrived.
public class TopicMapperShould : UnitTestBase
{
    private static TopicMapper CreateMapper()
    {
        var userMapper = new UserMapper();
        return new TopicMapper(userMapper, new BoardMapper(userMapper));
    }

    // The topic description is a BbText the client edits with X-Dm-Audience: author_edit, which the
    // converter honors only when the envelope names the author.
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

    // The domain resolves a board by alias, title or id and compares it with the current title, so
    // the field arrives as BoardTitle verbatim.
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
