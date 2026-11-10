using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.Chats;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Chats;

public class ChatServiceShould : UnitTestBase
{
    private readonly IIntentionManager intentionManager;
    private readonly IChatRepository repository;
    private readonly IUserReadRepository userRepository = Mock<IUserReadRepository>();
    private readonly IUnreadCountersRepository unreadCountersRepository;
    private readonly IIdentityProvider identityProvider;
    private readonly IUserBlacklistChecker userBlacklistChecker;
    private readonly IChatFactory factory;
    private readonly ChatService service;
    private readonly Guid currentUserId = Guid.NewGuid();
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly DateTimeOffset now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

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

        intentionManager = Mock<IIntentionManager>();

        factory = Mock<IChatFactory>();

        repository = Mock<IChatRepository>();
        repository.Create(Arg.Any<CreateChatEntity>(), Arg.Any<IEnumerable<CreateChatLinkEntity>>())
            .Returns(new Chat { Id = Guid.NewGuid() });

        unreadCountersRepository = Mock<IUnreadCountersRepository>();
        unreadCountersRepository.CreateMarkerAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<IEnumerable<Guid>>())
            .Returns(Task.CompletedTask);
        unreadCountersRepository.SelectByEntitiesAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<Guid[]>())
            .Returns(ci => ci.ArgAt<Guid[]>(2).ToDictionary(id => id, _ => 0));


        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId, "TestUser", UserRole.RegularUser,
            new UserSettings { PageSize = 20 }));

        dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(now);

        userBlacklistChecker = Mock<IUserBlacklistChecker>();
        userBlacklistChecker
            .GetOwnersBlockingIfFlagEnabledAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<UserBlacklistSettings>(), Arg.Any<CancellationToken>()).Returns(new HashSet<Guid>());

        service = new ChatService(
            createValidator,
            updateValidator,
            factory,
            repository,
            userRepository,
            unreadCountersRepository,
            intentionManager,
            identityProvider,
            userBlacklistChecker,
            dateTimeProvider);
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
        factory.CreateGroup(Arg.Any<string>(), Arg.Any<Guid[]>()).Returns((chatEntity, linkEntities));

        await service.CreateGroupAsync(createChat);

        await repository.Received(1).Create(chatEntity, linkEntities);
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
        factory.CreateGroup(Arg.Any<string>(), Arg.Any<Guid[]>()).Returns((chatEntity, linkEntities));
        repository.Create(Arg.Any<CreateChatEntity>(), Arg.Any<IEnumerable<CreateChatLinkEntity>>())
            .Returns(new Chat { Id = chatId });

        await service.CreateGroupAsync(createChat);

        await unreadCountersRepository.Received(1).CreateMarkerAsync(chatId, UnreadEntryType.Message, Arg.Any<IEnumerable<Guid>>());
        // The row landed, so the reservation was committed.
        await unreadCountersRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<IEnumerable<Guid>>());
    }

    /// <summary>
    /// The creator named among the participants and a participant named twice each get
    /// one link: the factory writes a link per identifier it is given.
    /// </summary>
    [Fact]
    public async Task PassEachParticipantToTheFactoryOnce()
    {
        var otherId = Guid.NewGuid();
        IEnumerable<Guid>? passed = null;
        factory.CreateGroup(Arg.Any<string>(), Arg.Do<IEnumerable<Guid>>(ids => passed = ids))
            .Returns((new CreateChatEntity(), new List<CreateChatLinkEntity>()));

        await service.CreateGroupAsync(new CreateChat
        {
            Title = "Test Group",
            ParticipantIds = new[] { otherId, currentUserId, otherId }
        });

        passed.Should().BeEquivalentTo(new[] { otherId, currentUserId });
    }

    // The expiry index reads the removal stamp, and an untouched marker carries none.
    [Fact]
    public async Task ForgetTheUnreadCounterOfAParticipantWhoWasRemoved()
    {
        var chatId = Guid.NewGuid();
        var removed = Guid.NewGuid();
        ChatToUpdate(chatId);

        await service.UpdateAsync(new UpdateChat { ChatId = chatId, RemoveParticipants = new[] { removed } });

        await unreadCountersRepository.Received(1).DeleteAsync(chatId, UnreadEntryType.Message,
                Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { removed })));
    }

    // The read behind it is the unfiltered one, so participation is checked here.
    [Theory]
    [InlineData(ChatType.Group)]
    [InlineData(ChatType.Direct)]
    public async Task RefuseToMarkAsReadAConversationTheCallerIsNotIn(ChatType type)
    {
        var chatId = Guid.NewGuid();
        repository.GetForUpdate(chatId).Returns(new Chat
        {
            Id = chatId,
            Type = type,
            Participants = new[] { new GeneralUser { UserId = Guid.NewGuid() } }
        });

        var act = async () => await service.MarkAsReadAsync(chatId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await unreadCountersRepository.DidNotReceive().FlushAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<Guid>());
    }

    // A game room chat has no participant rows by design, and its access was decided by the room
    // before this call.
    [Fact]
    public async Task StillMarkAGameRoomChatAsReadWithNoParticipantRows()
    {
        var chatId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        repository.GetForUpdate(chatId).Returns(new Chat
        {
            Id = chatId,
            Type = ChatType.GameRoom,
            RoomId = roomId,
            Participants = Array.Empty<GeneralUser>()
        });

        await service.MarkAsReadAsync(chatId);

        await unreadCountersRepository.Received(1).FlushAsync(currentUserId, UnreadEntryType.Message, roomId);
    }

    /// <summary>
    /// The header badge asks for one number and gets it from the markers, not
    /// from a page of conversations: the reader is their own parent, so every
    /// chat of theirs is in scope whether or not it fits on the first page.
    /// </summary>
    [Fact]
    public async Task CountUnreadMessagesOfEveryConversationUnderTheReaderAsParent()
    {
        unreadCountersRepository
            .SelectTotalUnreadByParentsAsync(currentUserId, UnreadEntryType.Message, currentUserId)
            .Returns(new Dictionary<Guid, int> { [currentUserId] = 17 });

        var count = await service.CountUnreadAsync();

        count.Should().Be(17);
        await repository.DidNotReceive().Get(Arg.Any<Guid>(), Arg.Any<PagingData>());
    }

    [Fact]
    public async Task AuthorizeUpdateChatAction()
    {
        var chatId = Guid.NewGuid();
        var chat = ChatToUpdate(chatId);

        await service.UpdateAsync(new UpdateChat { ChatId = chatId, Title = "Updated Title" });

        intentionManager.Received(1).ThrowIfForbidden(ChatIntention.UpdateChat, chat);
    }

    [Fact]
    public async Task CreateDirectChatWhenNotExists()
    {
        var otherUserId = Guid.NewGuid();
        var username = "testuser";
        var chatEntity = new CreateChatEntity();
        var linkEntities = new List<CreateChatLinkEntity>();
        factory.CreateDirect(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((chatEntity, linkEntities));
        userRepository.FindUserIdAsync(username).Returns(otherUserId);
        repository.FindDirectChat(currentUserId, otherUserId).Returns((Chat?)null);
        repository.Create(Arg.Any<CreateChatEntity>(), Arg.Any<IEnumerable<CreateChatLinkEntity>>())
            .Returns(new Chat { Id = Guid.NewGuid(), Type = ChatType.Direct });

        await service.GetOrCreateDirectAsync(username);

        await repository.Received(1).Create(chatEntity, linkEntities);
    }

    [Fact]
    public async Task ReturnExistingDirectChatWhenExists()
    {
        var otherUserId = Guid.NewGuid();
        var username = "testuser";
        var existingChat = new Chat { Id = Guid.NewGuid(), Type = ChatType.Direct };
        userRepository.FindUserIdAsync(username).Returns(otherUserId);
        repository.FindDirectChat(currentUserId, otherUserId).Returns(existingChat);

        await service.GetOrCreateDirectAsync(username);

        await repository.DidNotReceive().Create(Arg.Any<CreateChatEntity>(), Arg.Any<IEnumerable<CreateChatLinkEntity>>());
    }

    // The participant is refused before the chat exists, the message path holds the other half.
    [Fact]
    public async Task RefuseToCreateAGroupWithSomebodyWhoBlockedTheAuthor()
    {
        var blocker = Guid.NewGuid();
        userBlacklistChecker
            .GetOwnersBlockingIfFlagEnabledAsync(
                currentUserId, Arg.Any<IReadOnlyCollection<Guid>>(),
                UserBlacklistSettings.BlockDirectMessages, Arg.Any<CancellationToken>()).Returns(new HashSet<Guid> { blocker });
        var createChat = new CreateChat { Title = "Test Group", ParticipantIds = new[] { blocker } };

        var act = async () => await service.CreateGroupAsync(createChat);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await repository.DidNotReceive().Create(Arg.Any<CreateChatEntity>(), Arg.Any<IEnumerable<CreateChatLinkEntity>>());
    }

    // Any participant of a group may update it, so a door standing at creation alone is opened by
    // whoever adds the blocked party a minute later.
    [Fact]
    public async Task RefuseToAddSomebodyWhoBlockedTheAuthorToAnExistingGroup()
    {
        var chatId = Guid.NewGuid();
        var blocker = Guid.NewGuid();
        ChatToUpdate(chatId, ChatType.Group);
        userBlacklistChecker
            .GetOwnersBlockingIfFlagEnabledAsync(
                currentUserId, Arg.Any<IReadOnlyCollection<Guid>>(),
                UserBlacklistSettings.BlockDirectMessages, Arg.Any<CancellationToken>()).Returns(new HashSet<Guid> { blocker });
        var updateChat = new UpdateChat { ChatId = chatId, AddParticipants = new[] { blocker } };

        var act = async () => await service.UpdateAsync(updateChat);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await repository.DidNotReceive().Update(Arg.Any<UpdateChatEntity>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task AddTheLinksTheFactoryMakesForTheNewParticipantsOfTheChat()
    {
        var chatId = Guid.NewGuid();
        var newcomer = Guid.NewGuid();
        ChatToUpdate(chatId, ChatType.Group);
        var links = new[] { new CreateChatLinkEntity { UserChatLinkId = Guid.NewGuid(), ChatId = chatId, UserId = newcomer } };
        factory.CreateLinks(chatId, Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { newcomer })))
            .Returns(links);

        await service.UpdateAsync(new UpdateChat { ChatId = chatId, AddParticipants = new[] { newcomer } });

        await repository.Received(1).Update(
            Arg.Is<UpdateChatEntity>(update => update.AddLinks.SequenceEqual(links)), Arg.Any<Guid>());
    }

    private Chat ChatToUpdate(Guid chatId, ChatType type = default)
    {
        var chat = new Chat { Id = chatId, Type = type, Participants = [] };
        repository.GetForUpdate(chatId).Returns(chat);
        repository.Update(Arg.Any<UpdateChatEntity>(), Arg.Any<Guid>()).Returns(chat);
        return chat;
    }

    private Chat ChatOfTheCaller(Guid chatId, ChatType type)
    {
        var chat = new Chat
        {
            Id = chatId,
            Type = type,
            Participants = new[] { new GeneralUser { UserId = currentUserId }, new GeneralUser { UserId = Guid.NewGuid() } }
        };
        repository.Get(chatId, currentUserId).Returns(chat);
        return chat;
    }

    [Fact]
    public async Task LeaveAGroupAndForgetTheUnreadMarkerOfTheLeaver()
    {
        var chatId = Guid.NewGuid();
        var chat = ChatOfTheCaller(chatId, ChatType.Group);

        await service.LeaveAsync(chatId);

        intentionManager.Received(1).ThrowIfForbidden(ChatIntention.LeaveChat, chat);
        await repository.Received(1).Leave(chatId, currentUserId, now);
        await unreadCountersRepository.Received(1).DeleteAsync(chatId, UnreadEntryType.Message,
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { currentUserId })));
    }

    [Fact]
    public async Task ClearTheHistoryForTheCallerAndReadWhatWasUnread()
    {
        var chatId = Guid.NewGuid();
        var chat = ChatOfTheCaller(chatId, ChatType.Direct);

        await service.ClearHistoryAsync(chatId);

        intentionManager.Received(1).ThrowIfForbidden(ChatIntention.ClearHistory, chat);
        await repository.Received(1).ClearHistory(chatId, currentUserId, now);
        await unreadCountersRepository.Received(1).FlushAsync(currentUserId, UnreadEntryType.Message, chatId);
    }

    /// <summary>
    /// A conversation of other people is not found for the caller, here as on
    /// every read of it, and nothing is written.
    /// </summary>
    [Fact]
    public async Task RefuseToLeaveOrClearAConversationTheCallerIsNotIn()
    {
        var chatId = Guid.NewGuid();
        repository.Get(chatId, currentUserId).Returns((Chat?)null);

        var leave = async () => await service.LeaveAsync(chatId);
        var clear = async () => await service.ClearHistoryAsync(chatId);

        await leave.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await clear.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await repository.DidNotReceive().Leave(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
        await repository.DidNotReceive().ClearHistory(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
        await unreadCountersRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<IEnumerable<Guid>>());
        await unreadCountersRepository.DidNotReceive().FlushAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task WriteNothingTheIntentionRefuses()
    {
        var chatId = Guid.NewGuid();
        var chat = ChatOfTheCaller(chatId, ChatType.Direct);
        intentionManager
            .When(m => m.ThrowIfForbidden(Arg.Any<ChatIntention>(), chat))
            .Do(_ => throw new HttpException(HttpStatusCode.Forbidden, "refused"));

        var leave = async () => await service.LeaveAsync(chatId);
        var clear = async () => await service.ClearHistoryAsync(chatId);

        await leave.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await clear.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await repository.DidNotReceive().Leave(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
        await repository.DidNotReceive().ClearHistory(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task ReadTheUpdatedChatBackForItsAuthor()
    {
        var chatId = Guid.NewGuid();
        ChatToUpdate(chatId, ChatType.Group);

        await service.UpdateAsync(new UpdateChat { ChatId = chatId, Title = "Новое название" });

        await repository.Received(1).Update(Arg.Any<UpdateChatEntity>(), currentUserId);
    }
}
