using System;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Messaging;
using DM.Web.API.Features.Messaging.Chats;
using DM.Web.API.Shared.BbRendering;
using AwesomeAssertions;
using Xunit;
using DtoMessage = DM.Domain.Messaging.Features.Messages.Message;

namespace DM.Web.API.Tests.Features.Messaging;

public class MessagingMapperShould : UnitTestBase
{
    private MessagingMapper CreateMapper() =>
        new(new UserMapper(Mock<IImgproxyUrlBuilder>()));

    /// <summary>
    /// PATCH carries "not sent" as null and the write model must keep saying
    /// it: an empty participant list here would read as "remove everybody",
    /// so a mapper that defaults the collections re-introduces exactly that.
    /// </summary>
    [Fact]
    public void KeepUnsentParticipantListsNull()
    {
        var update = CreateMapper().ToUpdateChat(new UpdateChat
        {
            Title = "Только имя"
        });

        update.Title.Should().Be("Только имя");
        update.AddParticipants.Should().BeNull();
        update.RemoveParticipants.Should().BeNull();
    }

    [Fact]
    public void CarrySentParticipantListsThrough()
    {
        var userId = Guid.NewGuid();
        var update = CreateMapper().ToUpdateChat(new UpdateChat
        {
            AddParticipants = [userId]
        });

        update.AddParticipants.Should().ContainSingle().Which.Should().Be(userId);
        update.RemoveParticipants.Should().BeNull();
    }

    /// <summary>
    /// The text carrier is surface-specific: global chat gets the safe-image
    /// surface, direct chats the one with neither [mod] nor [private], and
    /// the envelope names the sender so the author_edit round-trip works.
    /// </summary>
    [Theory]
    [InlineData(ChatType.Global, typeof(GlobalChatBbText))]
    [InlineData(ChatType.Direct, typeof(DirectMessageBbText))]
    [InlineData(ChatType.Group, typeof(DirectMessageBbText))]
    [InlineData(ChatType.GameRoom, typeof(CommonBbText))]
    public void PickTheSurfaceByChatType(ChatType chatType, Type expectedCarrier)
    {
        var authorId = Guid.NewGuid();
        var message = CreateMapper().ToMessage(new DtoMessage
        {
            Id = Guid.NewGuid(),
            ChatType = chatType,
            Text = "привет",
            Author = new DM.Domain.Core.Dto.GeneralUser { UserId = authorId }
        });

        message.Text.Should().BeOfType(expectedCarrier);
        message.Text.Value.Should().Be("привет");
        message.Text.Context.Should().NotBeNull();
        message.Text.Context!.PostAuthorUserId.Should().Be(authorId);
    }
}
