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
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.Chats;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Chats;

public class ChatServiceShould : UnitTestBase
{
    private readonly IIntentionManager _intentionManager;
    private readonly IChatRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IChatFactory _factory;
    private readonly ChatService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public ChatServiceShould()
    {
        var createValidator = Mock<IValidator<CreateChat>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateChat>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateChat>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateChat>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _factory = Mock<IChatFactory>();

        _repository = Mock<IChatRepository>();
        _repository.Create(Arg.Any<CreateChatEntity>(), Arg.Any<IEnumerable<CreateChatLinkEntity>>())
            .Returns(new Chat { Id = Guid.NewGuid() });

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _unreadCountersRepository.CreateMarkerAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<IEnumerable<Guid>>())
            .Returns(Task.CompletedTask);
        _unreadCountersRepository.SelectByEntitiesAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<Guid[]>())
            .Returns(ci =>
            {
                var userId = ci.ArgAt<Guid>(0);
                var type = ci.ArgAt<UnreadEntryType>(1);
                var ids = ci.ArgAt<Guid[]>(2);
                return ids.ToDictionary(id => id, _ => 0);
            });

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        _identityProvider = Mock<IIdentityProvider>();
        var user = new AuthenticatedUser { UserId = _currentUserId };
        var settings = new UserSettings { Paging = new PagingSettings { MessagesPerPage = 20 } };
        var session = new Session();
        var identity = Identity.Success(user, session, settings, "token");
        _identityProvider.Current.Returns(identity);

        _userBlacklistChecker = Mock<IUserBlacklistChecker>();
        _userBlacklistChecker
            .GetOwnersBlockingIfFlagEnabledAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<UserBlacklistSettings>(), Arg.Any<CancellationToken>()).Returns(new HashSet<Guid>());

        _service = new ChatService(
            createValidator,
            updateValidator,
            _factory,
            _repository,
            _unreadCountersRepository,
            _intentionManager,
            guidFactory,
            _identityProvider,
            _userBlacklistChecker);
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
        _factory.CreateGroup(Arg.Any<string>(), Arg.Any<Guid[]>()).Returns((chatEntity, linkEntities));

        var result = await _service.CreateGroupAsync(createChat);

        await _repository.Received(1).Create(chatEntity, linkEntities);
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
        // The identifier comes off the entity the factory built, not off the row:
        // the markers are written before the row exists to return one.
        var chatEntity = new CreateChatEntity { ChatId = chatId };
        var linkEntities = new List<CreateChatLinkEntity>();
        _factory.CreateGroup(Arg.Any<string>(), Arg.Any<Guid[]>()).Returns((chatEntity, linkEntities));
        _repository.Create(Arg.Any<CreateChatEntity>(), Arg.Any<IEnumerable<CreateChatLinkEntity>>())
            .Returns(new Chat { Id = chatId });

        await _service.CreateGroupAsync(createChat);

        await _unreadCountersRepository.Received(1).CreateMarkerAsync(chatId, UnreadEntryType.Message, Arg.Any<IEnumerable<Guid>>());
        // The row landed, so the reservation was committed.
        await _unreadCountersRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<IEnumerable<Guid>>());
    }

    /// <summary>
    /// Somebody removed from a chat stops being counted for it.
    /// </summary>
    /// <remarks>
    /// A person could be counted into a chat two ways and out of it none. The
    /// link in Postgres was flagged removed, so the conversation vanished from
    /// every read of theirs, while the unread marker went on being incremented
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
        _repository.GetForUpdate(chatId).Returns(chat);
        _repository.Update(Arg.Any<UpdateChatEntity>()).Returns(chat);

        await _service.UpdateAsync(updateChat);

        await _unreadCountersRepository.Received(1).DeleteAsync(chatId, UnreadEntryType.Message,
                Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { removed })));
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
        _repository.GetForUpdate(chatId).Returns(new Chat
        {
            Id = chatId,
            Type = type,
            Participants = new[] { new GeneralUser { UserId = Guid.NewGuid() } }
        });

        var act = async () => await _service.MarkAsReadAsync(chatId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await _unreadCountersRepository.DidNotReceive().FlushAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<Guid>());
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
        _repository.GetForUpdate(chatId).Returns(new Chat
        {
            Id = chatId,
            Type = ChatType.GameRoom,
            RoomId = roomId,
            Participants = Array.Empty<GeneralUser>()
        });

        await _service.MarkAsReadAsync(chatId);

        await _unreadCountersRepository.Received(1).FlushAsync(_currentUserId, UnreadEntryType.Message, roomId);
    }

    [Fact]
    public async Task AuthorizeUpdateChatAction()
    {
        var chatId = Guid.NewGuid();
        var updateChat = new UpdateChat { ChatId = chatId, Title = "Updated Title" };
        var chat = new Chat { Id = chatId, Participants = Array.Empty<GeneralUser>() };
        _repository.GetForUpdate(chatId).Returns(chat);
        _repository.Update(Arg.Any<UpdateChatEntity>()).Returns(chat);

        await _service.UpdateAsync(updateChat);

        _intentionManager.Received(1).ThrowIfForbidden(ChatIntention.UpdateChat, chat);
    }

    [Fact]
    public async Task CreateDirectChatWhenNotExists()
    {
        var otherUserId = Guid.NewGuid();
        var username = "testuser";
        var chatEntity = new CreateChatEntity();
        var linkEntities = new List<CreateChatLinkEntity>();
        _factory.CreateDirect(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((chatEntity, linkEntities));
        _repository.FindUser(username).Returns(otherUserId);
        _repository.FindDirectChat(_currentUserId, otherUserId).Returns((Chat?)null);
        _repository.Create(Arg.Any<CreateChatEntity>(), Arg.Any<IEnumerable<CreateChatLinkEntity>>())
            .Returns(new Chat { Id = Guid.NewGuid(), Type = ChatType.Direct });

        var result = await _service.GetOrCreateDirectAsync(username);

        await _repository.Received(1).Create(chatEntity, linkEntities);
    }

    [Fact]
    public async Task ReturnExistingDirectChatWhenExists()
    {
        var otherUserId = Guid.NewGuid();
        var username = "testuser";
        var existingChat = new Chat { Id = Guid.NewGuid(), Type = ChatType.Direct };
        _repository.FindUser(username).Returns(otherUserId);
        _repository.FindDirectChat(_currentUserId, otherUserId).Returns(existingChat);

        var result = await _service.GetOrCreateDirectAsync(username);

        await _repository.DidNotReceive().Create(Arg.Any<CreateChatEntity>(), Arg.Any<IEnumerable<CreateChatLinkEntity>>());
    }

    /// <summary>
    /// A group chat is not the way around a personal blacklist.
    /// </summary>
    /// <remarks>
    /// The block on private messages was asked about for a direct chat alone, so
    /// somebody who had been blocked created a group with the same person and
    /// wrote there instead. This is the door: the participant is refused before
    /// the chat exists. The message path holds the other half, where a
    /// conversation of two is private correspondence whatever its type.
    /// </remarks>
    [Fact]
    public async Task RefuseToCreateAGroupWithSomebodyWhoBlockedTheAuthor()
    {
        var blocker = Guid.NewGuid();
        _userBlacklistChecker
            .GetOwnersBlockingIfFlagEnabledAsync(
                _currentUserId, Arg.Any<IReadOnlyCollection<Guid>>(),
                UserBlacklistSettings.BlockDirectMessages, Arg.Any<CancellationToken>()).Returns(new HashSet<Guid> { blocker });
        var createChat = new CreateChat { Title = "Test Group", ParticipantIds = new[] { blocker } };

        var act = async () => await _service.CreateGroupAsync(createChat);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().Create(Arg.Any<CreateChatEntity>(), Arg.Any<IEnumerable<CreateChatLinkEntity>>());
    }

    /// <summary>
    /// The same refusal when the group is already there and somebody is added to it.
    /// </summary>
    /// <remarks>
    /// Any participant of a group may update it, so a door standing at creation
    /// alone is opened by whoever adds the blocked party a minute later.
    /// </remarks>
    [Fact]
    public async Task RefuseToAddSomebodyWhoBlockedTheAuthorToAnExistingGroup()
    {
        var chatId = Guid.NewGuid();
        var blocker = Guid.NewGuid();
        var chat = new Chat { Id = chatId, Type = ChatType.Group, Participants = Array.Empty<GeneralUser>() };
        _repository.GetForUpdate(chatId).Returns(chat);
        _repository.Update(Arg.Any<UpdateChatEntity>()).Returns(chat);
        _userBlacklistChecker
            .GetOwnersBlockingIfFlagEnabledAsync(
                _currentUserId, Arg.Any<IReadOnlyCollection<Guid>>(),
                UserBlacklistSettings.BlockDirectMessages, Arg.Any<CancellationToken>()).Returns(new HashSet<Guid> { blocker });
        var updateChat = new UpdateChat { ChatId = chatId, AddParticipants = new[] { blocker } };

        var act = async () => await _service.UpdateAsync(updateChat);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().Update(Arg.Any<UpdateChatEntity>());
    }
}
