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
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Domain.Messaging.Features.Messages;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using DM.Domain.Account.Features.Authentication;
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Messages;

public class MessageServiceShould : UnitTestBase
{
    private readonly IChatService _chatService;
    private readonly IIntentionManager _intentionManager;
    private readonly IMessageRepository _repository;
    private readonly IEventProducer _eventProducer;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IMessageFactory _factory;
    private readonly MessageService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public MessageServiceShould()
    {
        var createValidator = Mock<IValidator<CreateMessage>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateMessage>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateMessage>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _chatService = Mock<IChatService>();
        _intentionManager = Mock<IIntentionManager>();

        _factory = Mock<IMessageFactory>();

        _repository = Mock<IMessageRepository>();
        _repository.Create(Arg.Any<CreateMessageEntity>(), Arg.Any<UpdateChatLastMessageEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Message { Id = Guid.NewGuid() });

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _unreadCountersRepository.IncrementExcludingAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<Guid>())
            .Returns(Task.CompletedTask);

        _eventProducer = Mock<IEventProducer>();
        _eventProducer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        var identityProvider = Mock<IIdentityProvider>();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = _currentUserId, Role = UserRole.RegularUser },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        identityProvider.Current.Returns(identity);

        _userBlacklistChecker = Mock<IUserBlacklistChecker>();
        _userBlacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(Arg.Any<Guid>(), Arg.Any<UserBlacklistSettings>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        var globalChatEventRepository = Mock<IGlobalChatEventRepository>();
        globalChatEventRepository.GetActiveEvent().Returns((GlobalChatEvent?)null);

        _service = new MessageService(
            _chatService,
            globalChatEventRepository,
            createValidator,
            updateValidator,
            _intentionManager,
            _factory,
            _repository,
            _unreadCountersRepository,
            _eventProducer,
            identityProvider,
            _userBlacklistChecker);
    }

    [Fact]
    public async Task AuthorizeCreateMessageAction()
    {
        var chatId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat { Id = chatId, Type = ChatType.Direct, Participants = Array.Empty<GeneralUser>() };
        var messageEntity = new CreateMessageEntity { MessageId = Guid.NewGuid() };
        _chatService.GetAsync(chatId).Returns(chat);
        _factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>()).Returns(messageEntity);

        await _service.CreateAsync(createMessage);

        _intentionManager.Received(1).ThrowIfForbidden(ChatIntention.CreateMessage, chat);
    }

    [Fact]
    public async Task SaveMessage()
    {
        var chatId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat { Id = chatId, Type = ChatType.Direct, Participants = Array.Empty<GeneralUser>() };
        var messageEntity = new CreateMessageEntity { MessageId = Guid.NewGuid() };
        _chatService.GetAsync(chatId).Returns(chat);
        _factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>()).Returns(messageEntity);

        await _service.CreateAsync(createMessage);

        await _repository.Received(1).Create(messageEntity, Arg.Any<UpdateChatLastMessageEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncrementUnreadCountersExcludingSender()
    {
        var chatId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat { Id = chatId, Type = ChatType.Direct, Participants = Array.Empty<GeneralUser>() };
        var messageEntity = new CreateMessageEntity { MessageId = Guid.NewGuid() };
        _chatService.GetAsync(chatId).Returns(chat);
        _factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>()).Returns(messageEntity);

        await _service.CreateAsync(createMessage);

        await _unreadCountersRepository.Received(1).IncrementExcludingAsync(chatId, UnreadEntryType.Message, _currentUserId);
    }

    [Fact]
    public async Task PublishNewMessageEvent()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat { Id = chatId, Type = ChatType.Direct, Participants = Array.Empty<GeneralUser>() };
        var messageEntity = new CreateMessageEntity { MessageId = messageId };
        _chatService.GetAsync(chatId).Returns(chat);
        _factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>()).Returns(messageEntity);
        _repository.Create(Arg.Any<CreateMessageEntity>(), Arg.Any<UpdateChatLastMessageEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Message { Id = messageId });

        await _service.CreateAsync(createMessage);

        await _eventProducer.Received(1).SendAsync(EventType.NewMessage, messageId);
    }

    [Fact]
    public async Task PublishNewGlobalChatMessageEventForGlobalChat()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat { Id = chatId, Type = ChatType.Global, Participants = Array.Empty<GeneralUser>() };
        var messageEntity = new CreateMessageEntity { MessageId = messageId };
        _chatService.GetAsync(chatId).Returns(chat);
        _factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>()).Returns(messageEntity);
        _repository.Create(Arg.Any<CreateMessageEntity>(), Arg.Any<UpdateChatLastMessageEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Message { Id = messageId });

        await _service.CreateAsync(createMessage);

        await _eventProducer.Received(1).SendAsync(EventType.NewMessage, messageId);
        await _eventProducer.Received(1).SendAsync(EventType.NewGlobalChatMessage, messageId);
    }

    [Fact]
    public async Task NotPublishGlobalChatMessageEventForDirectChat()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat { Id = chatId, Type = ChatType.Direct, Participants = Array.Empty<GeneralUser>() };
        var messageEntity = new CreateMessageEntity { MessageId = messageId };
        _chatService.GetAsync(chatId).Returns(chat);
        _factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>()).Returns(messageEntity);
        _repository.Create(Arg.Any<CreateMessageEntity>(), Arg.Any<UpdateChatLastMessageEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Message { Id = messageId });

        await _service.CreateAsync(createMessage);

        await _eventProducer.DidNotReceive().SendAsync(EventType.NewGlobalChatMessage, Arg.Any<Guid>());
    }

    /// <summary>
    /// A deleted message stops being counted.
    /// </summary>
    /// <remarks>
    /// Every other kind of comment on the site decrements here — blog,
    /// publication, topic, game, post and character. Messages did not, so the
    /// badge went on counting a message that no longer exists until something
    /// flushed the whole conversation.
    ///
    /// Addressed by the chat's counter identifier, not by its own: for a game
    /// room chat those are different, and using the chat's own is exactly how
    /// the room's unread went dead before.
    /// </remarks>
    [Fact]
    public async Task TakeTheUnreadCounterDownWithTheDeletedMessage()
    {
        var messageId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        var message = new Message { Id = messageId, ChatId = chatId, ChatType = ChatType.GameRoom, CreatedUtc = createdUtc };
        _repository.Get(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(message);
        _repository.Delete(messageId, _currentUserId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _chatService.GetGameRoomAsync(chatId)
            .Returns(new Chat { Id = chatId, RoomId = roomId, Type = ChatType.GameRoom });

        await _service.DeleteAsync(messageId);

        await _unreadCountersRepository.Received(1).DecrementAsync(roomId, UnreadEntryType.Message, createdUtc);
    }

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var messageId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var message = new Message { Id = messageId, ChatId = chatId };
        _repository.Get(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(message);
        _repository.Delete(messageId, _currentUserId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _chatService.GetAsync(chatId).Returns(new Chat { Id = chatId });

        await _service.DeleteAsync(messageId);

        _intentionManager.Received(1).ThrowIfForbidden(MessageIntention.Delete, message);
    }

    /// <summary>
    /// A group chat of two is private correspondence, and the block on private
    /// messages holds there.
    /// </summary>
    /// <remarks>
    /// The check asked about the direct type alone, so somebody who had been
    /// blocked created a group with the same person and wrote to them in it. What
    /// makes a conversation private is the two people in it, not the type it was
    /// created under.
    /// </remarks>
    [Fact]
    public async Task RefuseAMessageToAGroupOfTwoWhoseOtherMemberBlockedTheSender()
    {
        var chatId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat
        {
            Id = chatId,
            Type = ChatType.Group,
            Participants = new[]
            {
                new GeneralUser { UserId = _currentUserId },
                new GeneralUser { UserId = otherUserId }
            }
        };
        _chatService.GetAsync(chatId).Returns(chat);
        _factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>())
            .Returns(new CreateMessageEntity { MessageId = Guid.NewGuid() });
        _userBlacklistChecker
            .GetBlockedUserIdsIfFlagEnabledAsync(
                otherUserId, UserBlacklistSettings.BlockDirectMessages, Arg.Any<CancellationToken>()).Returns(new HashSet<Guid> { _currentUserId });

        var act = async () => await _service.CreateAsync(createMessage);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().Create(Arg.Any<CreateMessageEntity>(), Arg.Any<UpdateChatLastMessageEntity>(),
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A group with a third person in it is not private correspondence, and the
    /// setting says nothing about it.
    /// </summary>
    /// <remarks>
    /// The setting is about writing to one addressee. Reading it as "never in the
    /// same room" would silence a conversation the other participants are part of,
    /// which is what the door check in ChatService decides instead, once, when the
    /// person is put there.
    /// </remarks>
    [Fact]
    public async Task StillDeliverToAGroupOfThreeWhereOneMemberBlockedTheSender()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var blockerId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat
        {
            Id = chatId,
            Type = ChatType.Group,
            Participants = new[]
            {
                new GeneralUser { UserId = _currentUserId },
                new GeneralUser { UserId = blockerId },
                new GeneralUser { UserId = Guid.NewGuid() }
            }
        };
        _chatService.GetAsync(chatId).Returns(chat);
        _factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>())
            .Returns(new CreateMessageEntity { MessageId = messageId });
        _userBlacklistChecker
            .GetBlockedUserIdsIfFlagEnabledAsync(
                blockerId, UserBlacklistSettings.BlockDirectMessages, Arg.Any<CancellationToken>()).Returns(new HashSet<Guid> { _currentUserId });

        await _service.CreateAsync(createMessage);

        await _repository.Received(1).Create(Arg.Any<CreateMessageEntity>(), Arg.Any<UpdateChatLastMessageEntity>(),
                Arg.Any<CancellationToken>());
    }
}
