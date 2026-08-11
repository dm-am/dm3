using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.Chats;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Chats;

public class ChatServiceShould : UnitTestBase
{
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IChatRepository> _repository;
    private readonly Mock<IUnreadCountersRepository> _unreadCountersRepository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly ISetup<IChatFactory, (CreateChatEntity, IEnumerable<CreateChatLinkEntity>)> _createGroupSetup;
    private readonly ISetup<IChatFactory, (CreateChatEntity, IEnumerable<CreateChatLinkEntity>)> _createDirectSetup;
    private readonly ChatService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public ChatServiceShould()
    {
        var createValidator = Mock<IValidator<CreateChat>>();
        createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateChat>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateChat>>();
        updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateChat>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<ChatIntention>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<ChatIntention>(), It.IsAny<Chat>()));

        var factory = Mock<IChatFactory>();
        _createGroupSetup = factory.Setup(f => f.CreateGroup(It.IsAny<string>(), It.IsAny<Guid[]>()));
        _createDirectSetup = factory.Setup(f => f.CreateDirect(It.IsAny<Guid>(), It.IsAny<Guid>()));

        _repository = Mock<IChatRepository>();
        _repository.Setup(r => r.Create(It.IsAny<CreateChatEntity>(), It.IsAny<IEnumerable<CreateChatLinkEntity>>()))
            .ReturnsAsync(new Chat { Id = Guid.NewGuid() });

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _unreadCountersRepository.Setup(r => r.CreateAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>(), It.IsAny<IEnumerable<Guid>>()))
            .Returns(Task.CompletedTask);
        _unreadCountersRepository.Setup(r => r.SelectByEntitiesAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .ReturnsAsync((Guid userId, UnreadEntryType type, Guid[] ids) =>
                ids.ToDictionary(id => id, _ => 0));

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _identityProvider = Mock<IIdentityProvider>();
        var user = new AuthenticatedUser { UserId = _currentUserId };
        var settings = new UserSettings { Paging = new PagingSettings { MessagesPerPage = 20 } };
        var session = new Session();
        var identity = Identity.Success(user, session, settings, "token");
        _identityProvider.Setup(p => p.Current).Returns(identity);

        _service = new ChatService(
            createValidator.Object,
            updateValidator.Object,
            factory.Object,
            _repository.Object,
            _unreadCountersRepository.Object,
            _intentionManager.Object,
            guidFactory.Object,
            _identityProvider.Object);
    }

    [Fact]
    public async Task CreateGroupChatWithParticipants()
    {
        var createChat = new CreateChat
        {
            Title = "Test Group",
            ParticipantIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
        };
        var chatEntity = new CreateChatEntity();
        var linkEntities = new List<CreateChatLinkEntity>();
        _createGroupSetup.Returns((chatEntity, linkEntities));

        var result = await _service.CreateGroupAsync(createChat);

        _repository.Verify(r => r.Create(chatEntity, linkEntities), Times.Once);
    }

    [Fact]
    public async Task CreateUnreadCountersForAllParticipantsOnGroupCreation()
    {
        var createChat = new CreateChat
        {
            Title = "Test Group",
            ParticipantIds = new[] { Guid.NewGuid(), Guid.NewGuid() }
        };
        var chatId = Guid.NewGuid();
        var chatEntity = new CreateChatEntity();
        var linkEntities = new List<CreateChatLinkEntity>();
        _createGroupSetup.Returns((chatEntity, linkEntities));
        _repository.Setup(r => r.Create(It.IsAny<CreateChatEntity>(), It.IsAny<IEnumerable<CreateChatLinkEntity>>()))
            .ReturnsAsync(new Chat { Id = chatId });

        await _service.CreateGroupAsync(createChat);

        _unreadCountersRepository.Verify(
            r => r.CreateAsync(chatId, UnreadEntryType.Message, It.IsAny<IEnumerable<Guid>>()),
            Times.Once);
    }

    /// <summary>
    /// Somebody removed from a chat stops being counted for it.
    /// </summary>
    /// <remarks>
    /// A person could be counted into a chat two ways and out of it none. The
    /// link in Postgres was flagged removed, so the conversation vanished from
    /// every read of theirs, while the marker in Mongo went on being incremented
    /// by every later message — and nothing collected it, because the expiry
    /// index reads the removal stamp and an untouched marker carries none.
    /// </remarks>
    [Fact]
    public async Task ForgetTheUnreadCounterOfAParticipantWhoWasRemoved()
    {
        var chatId = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var chat = new Chat { Id = chatId, Participants = Array.Empty<GeneralUser>() };
        var updateChat = new UpdateChat { ChatId = chatId, RemoveParticipants = new[] { removed } };
        _repository.Setup(r => r.GetForUpdate(chatId)).ReturnsAsync(chat);
        _repository.Setup(r => r.Update(It.IsAny<UpdateChatEntity>())).ReturnsAsync(chat);

        await _service.UpdateAsync(updateChat);

        _unreadCountersRepository.Verify(
            r => r.DeleteAsync(chatId, UnreadEntryType.Message,
                It.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { removed }))),
            Times.Once);
    }

    /// <summary>
    /// Marking a conversation read is a thing only its participants may do.
    /// </summary>
    /// <remarks>
    /// The read behind it is the unfiltered one, so the endpoint accepted any
    /// identifier somebody remembered. Harmless while the marker of a removed
    /// participant stayed live and this only zeroed it; once removal leaves a
    /// tombstone, the flush would find no marker of their own, borrow a
    /// neighbour's parent and write a live document with no removal stamp —
    /// which nothing collects afterwards.
    /// </remarks>
    [Theory]
    [InlineData(ChatType.Group)]
    [InlineData(ChatType.Direct)]
    public async Task RefuseToMarkAsReadAConversationTheCallerIsNotIn(ChatType type)
    {
        var chatId = Guid.NewGuid();
        _repository.Setup(r => r.GetForUpdate(chatId)).ReturnsAsync(new Chat
        {
            Id = chatId,
            Type = type,
            Participants = new[] { new GeneralUser { UserId = Guid.NewGuid() } }
        });

        var act = async () => await _service.MarkAsReadAsync(chatId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        _unreadCountersRepository.Verify(
            r => r.FlushAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>(), It.IsAny<Guid>()),
            Times.Never);
    }

    /// <summary>
    /// A game room chat has no participant rows by design, and its access was
    /// decided by the room before this call.
    /// </summary>
    [Fact]
    public async Task StillMarkAGameRoomChatAsReadWithNoParticipantRows()
    {
        var chatId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        _repository.Setup(r => r.GetForUpdate(chatId)).ReturnsAsync(new Chat
        {
            Id = chatId,
            Type = ChatType.GameRoom,
            RoomId = roomId,
            Participants = Array.Empty<GeneralUser>()
        });

        await _service.MarkAsReadAsync(chatId);

        _unreadCountersRepository.Verify(
            r => r.FlushAsync(_currentUserId, UnreadEntryType.Message, roomId), Times.Once);
    }

    [Fact]
    public async Task AuthorizeUpdateChatAction()
    {
        var chatId = Guid.NewGuid();
        var updateChat = new UpdateChat { ChatId = chatId, Title = "Updated Title" };
        var chat = new Chat { Id = chatId, Participants = Array.Empty<GeneralUser>() };
        _repository.Setup(r => r.GetForUpdate(chatId)).ReturnsAsync(chat);
        _repository.Setup(r => r.Update(It.IsAny<UpdateChatEntity>())).ReturnsAsync(chat);

        await _service.UpdateAsync(updateChat);

        _intentionManager.Verify(m => m.ThrowIfForbidden(ChatIntention.UpdateChat, chat), Times.Once);
    }

    [Fact]
    public async Task CreateDirectChatWhenNotExists()
    {
        var otherUserId = Guid.NewGuid();
        var username = "testuser";
        var chatEntity = new CreateChatEntity();
        var linkEntities = new List<CreateChatLinkEntity>();
        _createDirectSetup.Returns((chatEntity, linkEntities));
        _repository.Setup(r => r.FindUser(username)).ReturnsAsync(otherUserId);
        _repository.Setup(r => r.FindDirectChat(_currentUserId, otherUserId)).ReturnsAsync((Chat?)null);
        _repository.Setup(r => r.Create(It.IsAny<CreateChatEntity>(), It.IsAny<IEnumerable<CreateChatLinkEntity>>()))
            .ReturnsAsync(new Chat { Id = Guid.NewGuid(), Type = ChatType.Direct });

        var result = await _service.GetOrCreateDirectAsync(username);

        _repository.Verify(r => r.Create(chatEntity, linkEntities), Times.Once);
    }

    [Fact]
    public async Task ReturnExistingDirectChatWhenExists()
    {
        var otherUserId = Guid.NewGuid();
        var username = "testuser";
        var existingChat = new Chat { Id = Guid.NewGuid(), Type = ChatType.Direct };
        _repository.Setup(r => r.FindUser(username)).ReturnsAsync(otherUserId);
        _repository.Setup(r => r.FindDirectChat(_currentUserId, otherUserId)).ReturnsAsync(existingChat);

        var result = await _service.GetOrCreateDirectAsync(username);

        _repository.Verify(r => r.Create(It.IsAny<CreateChatEntity>(), It.IsAny<IEnumerable<CreateChatLinkEntity>>()), Times.Never);
    }
}
