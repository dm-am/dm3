using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Content;
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
    private readonly IBbTagNameCase _tagNames;
    private readonly IGlobalChatEventRepository _globalChatEventRepository;
    private readonly IIdentityProvider _identityProvider;
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

        var identityProvider = _identityProvider = Mock<IIdentityProvider>();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = _currentUserId, Role = UserRole.RegularUser },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        identityProvider.Current.Returns(identity);

        _userBlacklistChecker = Mock<IUserBlacklistChecker>();
        _userBlacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(Arg.Any<Guid>(), Arg.Any<UserBlacklistSettings>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        var globalChatEventRepository = _globalChatEventRepository = Mock<IGlobalChatEventRepository>();
        globalChatEventRepository.GetActiveEvent().Returns((GlobalChatEvent?)null);

        // The text as it came, unless a test says otherwise.
        _tagNames = Mock<IBbTagNameCase>();
        _tagNames.ToLowerCase(Arg.Any<string?>(), Arg.Any<BbTagSet>()).Returns(ci => ci.ArgAt<string?>(0));

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
            _userBlacklistChecker,
            _tagNames);
    }

    /// <summary>
    /// The save path writes the names of the tags in lower case, [mod] among them
    /// only in the chats that draw it: the global chat and a game room, and not a
    /// direct or a group chat, where [MOD] is text and keeps its case.
    /// </summary>
    [Theory]
    [InlineData(ChatType.Global, BbTagSet.WithModBlock)]
    [InlineData(ChatType.GameRoom, BbTagSet.WithModBlock)]
    [InlineData(ChatType.Direct, BbTagSet.Common)]
    [InlineData(ChatType.Group, BbTagSet.Common)]
    public async Task WriteTheTagNamesInLowerCase_WithTheTagsOfTheChat(ChatType type, BbTagSet tags)
    {
        var chatId = Guid.NewGuid();
        var chat = new Chat { Id = chatId, Type = type, Participants = Array.Empty<GeneralUser>() };
        _chatService.GetAsync(chatId).Returns(chat);
        _tagNames.ToLowerCase("[B]текст[/B]", tags).Returns("[b]текст[/b]");
        _factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>())
            .Returns(ci => new CreateMessageEntity { MessageId = Guid.NewGuid(), Text = ci.ArgAt<CreateMessage>(0).Text });

        await _service.CreateAsync(new CreateMessage { ChatId = chatId, Text = "[B]текст[/B]" });

        await _repository.Received(1).Create(
            Arg.Is<CreateMessageEntity>(entity => entity.Text == "[b]текст[/b]"),
            Arg.Any<UpdateChatLastMessageEntity>(), Arg.Any<CancellationToken>());
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

    // ═══ WHAT THE PAGES OFFER ═══

    /// <summary>The shape of a chat the field is asked about.</summary>
    public enum Shape
    {
        DirectOpen,
        DirectBlocked,
        GroupOfTwoBlocked,
        GroupOfThreeBlocked,
        GlobalNoEvent,
        GlobalClosedEventOutside,
        GlobalClosedEventInside,
        GlobalOpenEvent
    }

    /// <summary>
    /// The page of a correspondence offers its field by MayCreateAsync, and the
    /// answer is false exactly where CreateAsync refuses: every case asks both
    /// about the same chat, the intention let through, so what is compared is
    /// the rest of the rule - the one addressee of a conversation of two, and a
    /// closed event of the global chat.
    /// </summary>
    [Theory]
    [InlineData(Shape.DirectOpen, true)]
    [InlineData(Shape.DirectBlocked, false)]
    [InlineData(Shape.GroupOfTwoBlocked, false)]
    [InlineData(Shape.GroupOfThreeBlocked, true)]
    [InlineData(Shape.GlobalNoEvent, true)]
    [InlineData(Shape.GlobalClosedEventOutside, false)]
    [InlineData(Shape.GlobalClosedEventInside, true)]
    [InlineData(Shape.GlobalOpenEvent, true)]
    public async Task OfferTheFieldExactlyWhereTheMessageIsTaken(Shape shape, bool expected)
    {
        var blocker = Guid.NewGuid();
        var chat = shape switch
        {
            Shape.DirectOpen or Shape.DirectBlocked => Conversation(ChatType.Direct, blocker),
            Shape.GroupOfTwoBlocked => Conversation(ChatType.Group, blocker),
            Shape.GroupOfThreeBlocked => Conversation(ChatType.Group, blocker, Guid.NewGuid()),
            _ => new Chat { Id = Guid.NewGuid(), Type = ChatType.Global, Participants = [] }
        };
        if (shape is Shape.DirectBlocked or Shape.GroupOfTwoBlocked or Shape.GroupOfThreeBlocked)
        {
            BlocksTheWriter(blocker);
        }

        if (shape is Shape.GlobalClosedEventOutside or Shape.GlobalClosedEventInside or Shape.GlobalOpenEvent)
        {
            _globalChatEventRepository.GetActiveEvent().Returns(new GlobalChatEvent
            {
                Id = Guid.NewGuid(),
                IsOpen = shape == Shape.GlobalOpenEvent,
                Participants = shape == Shape.GlobalClosedEventInside
                    ? [new GlobalChatEventParticipant { User = new GeneralUser { UserId = _currentUserId } }]
                    : [new GlobalChatEventParticipant { User = new GeneralUser { UserId = Guid.NewGuid() } }]
            });
        }

        _intentionManager.IsAllowed(ChatIntention.CreateMessage, chat).Returns(true);

        var offered = await _service.MayCreateAsync(chat);
        var taken = await IsTakenAsync(chat);

        offered.Should().Be(expected);
        taken.Should().Be(offered, "the field is offered by the answer the message is taken by");
    }

    /// <summary>
    /// The intention comes first: a reader the chat refuses (not a participant,
    /// a banned reader of the global chat) is offered no field, whatever the rest
    /// of the rule would say.
    /// </summary>
    [Fact]
    public async Task OfferNoFieldWhereTheIntentionRefuses()
    {
        var chat = Conversation(ChatType.Direct, Guid.NewGuid());
        _intentionManager.IsAllowed(ChatIntention.CreateMessage, chat).Returns(false);

        (await _service.MayCreateAsync(chat)).Should().BeFalse();
    }

    /// <summary>
    /// The profile offers "Написать сообщение" by MayWriteToAsync, asked before the
    /// conversation exists, and the answer is the one the first message in a
    /// conversation of the two gets.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task OfferTheMessageExactlyWhereAConversationOfTheTwoTakesIt(bool blocks, bool expected)
    {
        var addressee = Guid.NewGuid();
        if (blocks)
        {
            BlocksTheWriter(addressee);
        }

        var chat = Conversation(ChatType.Direct, addressee);
        _intentionManager.IsAllowed(ChatIntention.CreateMessage, chat).Returns(true);

        var offered = await _service.MayWriteToAsync(addressee);

        offered.Should().Be(expected);
        (await IsTakenAsync(chat)).Should().Be(offered);
        (await _service.MayCreateAsync(chat)).Should().Be(offered);
    }

    /// <summary>
    /// Nobody is offered a message to himself, and a guest is offered none: he
    /// has no conversation to write it in.
    /// </summary>
    [Fact]
    public async Task OfferNoMessageToOneselfOrToAGuest()
    {
        (await _service.MayWriteToAsync(_currentUserId)).Should().BeFalse();

        var guest = Mock<IIdentity>();
        guest.User.Returns(AuthenticatedUser.Guest);
        _identityProvider.Current.Returns(guest);

        (await _service.MayWriteToAsync(Guid.NewGuid())).Should().BeFalse();
    }

    private Chat Conversation(ChatType type, params Guid[] others) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Participants = others.Prepend(_currentUserId).Select(id => new GeneralUser { UserId = id }).ToArray()
    };

    /// <summary>The user keeps the writer on his list with private messages blocked.</summary>
    private void BlocksTheWriter(Guid ownerId) =>
        _userBlacklistChecker
            .GetBlockedUserIdsIfFlagEnabledAsync(
                ownerId, UserBlacklistSettings.BlockDirectMessages, Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { _currentUserId });

    /// <summary>Whether CreateAsync takes a message in the chat or refuses it with 403.</summary>
    private async Task<bool> IsTakenAsync(Chat chat)
    {
        _chatService.GetAsync(chat.Id).Returns(chat);
        _factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>())
            .Returns(new CreateMessageEntity { MessageId = Guid.NewGuid() });
        try
        {
            await _service.CreateAsync(new CreateMessage { ChatId = chat.Id, Text = "Привет" });
            return true;
        }
        catch (HttpException e) when (e.StatusCode == HttpStatusCode.Forbidden)
        {
            return false;
        }
    }
}
