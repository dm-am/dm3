using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
using DM.Testing.Dsl;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Messages;

public class MessageServiceShould : UnitTestBase
{
    private readonly IChatService chatService;
    private readonly IIntentionManager intentionManager;
    private readonly IMessageRepository repository;
    private readonly IEventProducer eventProducer;
    private readonly IUnreadCountersRepository unreadCountersRepository;
    private readonly IUserBlacklistChecker userBlacklistChecker;
    private readonly IMessageFactory factory;
    private readonly IGlobalChatEventRepository globalChatEventRepository;
    private readonly IIdentityProvider identityProvider;
    private readonly MessageService service;
    private readonly Guid currentUserId = Guid.NewGuid();

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

        chatService = Mock<IChatService>();
        intentionManager = Mock<IIntentionManager>();

        factory = Mock<IMessageFactory>();

        repository = Mock<IMessageRepository>();
        repository.Create(Arg.Any<CreateMessageEntity>(), Arg.Any<UpdateChatLastMessageEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Message { Id = Guid.NewGuid() });

        unreadCountersRepository = Mock<IUnreadCountersRepository>();
        unreadCountersRepository.IncrementExcludingAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<Guid>())
            .Returns(Task.CompletedTask);

        eventProducer = Mock<IEventProducer>();
        eventProducer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId));

        userBlacklistChecker = Mock<IUserBlacklistChecker>();
        userBlacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(Arg.Any<Guid>(), Arg.Any<UserBlacklistSettings>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());

        globalChatEventRepository = Mock<IGlobalChatEventRepository>();
        globalChatEventRepository.GetActiveEvent().Returns((GlobalChatEvent?)null);

        // The text as it came, unless a test says otherwise.

        service = new MessageService(
            chatService,
            globalChatEventRepository,
            createValidator,
            updateValidator,
            intentionManager,
            factory,
            repository,
            unreadCountersRepository,
            eventProducer,
            identityProvider,
            userBlacklistChecker);
    }

    /// <summary>
    /// The text is stored as typed in every kind of chat: the spaces around it and
    /// the case of its tag names (BBCODE_RENDERING.md, "Регистр имен").
    /// </summary>
    [Theory]
    [InlineData(ChatType.Global)]
    [InlineData(ChatType.GameRoom)]
    [InlineData(ChatType.Direct)]
    [InlineData(ChatType.Group)]
    public async Task StoreTheTextAsTyped(ChatType type)
    {
        var chatId = Guid.NewGuid();
        var chat = new Chat { Id = chatId, Type = type, Participants = Array.Empty<GeneralUser>() };
        chatService.GetAsync(chatId).Returns(chat);
        factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>())
            .Returns(ci => new CreateMessageEntity { MessageId = Guid.NewGuid(), Text = ci.ArgAt<CreateMessage>(0).Text });

        await service.CreateAsync(new CreateMessage { ChatId = chatId, Text = AsTyped });

        await repository.Received(1).Create(
            Arg.Is<CreateMessageEntity>(entity => entity.Text == AsTyped),
            Arg.Any<UpdateChatLastMessageEntity>(), Arg.Any<CancellationToken>());
    }

    private const string AsTyped = "  [B]текст[/B]\n";

    [Fact]
    public async Task AuthorizeCreateMessageAction()
    {
        var (chat, _) = ArrangeCreate(ChatType.Direct);

        await service.CreateAsync(new CreateMessage { ChatId = chat.Id, Text = "Hello" });

        intentionManager.Received(1).ThrowIfForbidden(ChatIntention.CreateMessage, chat);
    }

    [Fact]
    public async Task SaveMessage()
    {
        var (chat, messageEntity) = ArrangeCreate(ChatType.Direct);

        await service.CreateAsync(new CreateMessage { ChatId = chat.Id, Text = "Hello" });

        await repository.Received(1).Create(messageEntity, Arg.Any<UpdateChatLastMessageEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IncrementUnreadCountersExcludingSender()
    {
        var (chat, _) = ArrangeCreate(ChatType.Direct);

        await service.CreateAsync(new CreateMessage { ChatId = chat.Id, Text = "Hello" });

        await unreadCountersRepository.Received(1).IncrementExcludingAsync(chat.Id, UnreadEntryType.Message, currentUserId);
    }

    [Fact]
    public async Task PublishNewMessageEvent()
    {
        var (chat, messageEntity) = ArrangeCreate(ChatType.Direct);

        await service.CreateAsync(new CreateMessage { ChatId = chat.Id, Text = "Hello" });

        await eventProducer.Received(1).SendAsync(EventType.NewMessage, messageEntity.MessageId);
    }

    [Fact]
    public async Task PublishNewGlobalChatMessageEventForGlobalChat()
    {
        var (chat, messageEntity) = ArrangeCreate(ChatType.Global);

        await service.CreateAsync(new CreateMessage { ChatId = chat.Id, Text = "Hello" });

        await eventProducer.Received(1).SendAsync(EventType.NewMessage, messageEntity.MessageId);
        await eventProducer.Received(1).SendAsync(EventType.NewGlobalChatMessage, messageEntity.MessageId);
    }

    [Fact]
    public async Task NotPublishGlobalChatMessageEventForDirectChat()
    {
        var (chat, _) = ArrangeCreate(ChatType.Direct);

        await service.CreateAsync(new CreateMessage { ChatId = chat.Id, Text = "Hello" });

        await eventProducer.DidNotReceive().SendAsync(EventType.NewGlobalChatMessage, Arg.Any<Guid>());
    }

    private (Chat Chat, CreateMessageEntity Entity) ArrangeCreate(ChatType type)
    {
        var chat = new Chat { Id = Guid.NewGuid(), Type = type, Participants = [] };
        var messageEntity = new CreateMessageEntity { MessageId = Guid.NewGuid() };
        chatService.GetAsync(chat.Id).Returns(chat);
        factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>()).Returns(messageEntity);
        repository.Create(Arg.Any<CreateMessageEntity>(), Arg.Any<UpdateChatLastMessageEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Message { Id = messageEntity.MessageId });
        return (chat, messageEntity);
    }

    // Addressed by the chat's counter identifier, which differs from its own for a game room chat.
    [Fact]
    public async Task TakeTheUnreadCounterDownWithTheDeletedMessage()
    {
        var messageId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        var message = new Message { Id = messageId, ChatId = chatId, ChatType = ChatType.GameRoom, CreatedUtc = createdUtc };
        repository.Get(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(message);
        repository.Delete(messageId, currentUserId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        chatService.GetGameRoomAsync(chatId)
            .Returns(new Chat { Id = chatId, RoomId = roomId, Type = ChatType.GameRoom });

        await service.DeleteAsync(messageId);

        await unreadCountersRepository.Received(1).DecrementAsync(roomId, UnreadEntryType.Message, createdUtc);
    }

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var messageId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var message = new Message { Id = messageId, ChatId = chatId };
        repository.Get(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(message);
        repository.Delete(messageId, currentUserId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        chatService.GetAsync(chatId).Returns(new Chat { Id = chatId });

        await service.DeleteAsync(messageId);

        intentionManager.Received(1).ThrowIfForbidden(MessageIntention.Delete, message);
    }

    // What makes a conversation private is the two people in it, not its type.
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
                new GeneralUser { UserId = currentUserId },
                new GeneralUser { UserId = otherUserId }
            }
        };
        chatService.GetAsync(chatId).Returns(chat);
        factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>())
            .Returns(new CreateMessageEntity { MessageId = Guid.NewGuid() });
        userBlacklistChecker
            .GetBlockedUserIdsIfFlagEnabledAsync(
                otherUserId, UserBlacklistSettings.BlockDirectMessages, Arg.Any<CancellationToken>()).Returns(new HashSet<Guid> { currentUserId });

        var act = async () => await service.CreateAsync(createMessage);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await repository.DidNotReceive().Create(Arg.Any<CreateMessageEntity>(), Arg.Any<UpdateChatLastMessageEntity>(),
                Arg.Any<CancellationToken>());
    }

    // The setting is about writing to one addressee, the door check in ChatService decides the
    // rest.
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
                new GeneralUser { UserId = currentUserId },
                new GeneralUser { UserId = blockerId },
                new GeneralUser { UserId = Guid.NewGuid() }
            }
        };
        chatService.GetAsync(chatId).Returns(chat);
        factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>())
            .Returns(new CreateMessageEntity { MessageId = messageId });
        userBlacklistChecker
            .GetBlockedUserIdsIfFlagEnabledAsync(
                blockerId, UserBlacklistSettings.BlockDirectMessages, Arg.Any<CancellationToken>()).Returns(new HashSet<Guid> { currentUserId });

        await service.CreateAsync(createMessage);

        await repository.Received(1).Create(Arg.Any<CreateMessageEntity>(), Arg.Any<UpdateChatLastMessageEntity>(),
                Arg.Any<CancellationToken>());
    }

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
            globalChatEventRepository.GetActiveEvent().Returns(new GlobalChatEvent
            {
                Id = Guid.NewGuid(),
                IsOpen = shape == Shape.GlobalOpenEvent,
                Participants = shape == Shape.GlobalClosedEventInside
                    ? [new GlobalChatEventParticipant { User = new GeneralUser { UserId = currentUserId } }]
                    : [new GlobalChatEventParticipant { User = new GeneralUser { UserId = Guid.NewGuid() } }]
            });
        }

        intentionManager.IsAllowed(ChatIntention.CreateMessage, chat).Returns(true);

        var offered = await service.MayCreateAsync(chat);
        var taken = await IsTakenAsync(chat);

        offered.Should().Be(expected);
        taken.Should().Be(offered, "the field is offered by the answer the message is taken by");
    }

    [Fact]
    public async Task OfferNoFieldWhereTheIntentionRefuses()
    {
        var chat = Conversation(ChatType.Direct, Guid.NewGuid());
        intentionManager.IsAllowed(ChatIntention.CreateMessage, chat).Returns(false);

        (await service.MayCreateAsync(chat)).Should().BeFalse();
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
        intentionManager.IsAllowed(ChatIntention.CreateMessage, chat).Returns(true);

        var offered = await service.MayWriteToAsync(addressee);

        offered.Should().Be(expected);
        (await IsTakenAsync(chat)).Should().Be(offered);
        (await service.MayCreateAsync(chat)).Should().Be(offered);
    }

    /// <summary>
    /// Nobody is offered a message to himself, and a guest is offered none: he
    /// has no conversation to write it in.
    /// </summary>
    [Fact]
    public async Task OfferNoMessageToOneselfOrToAGuest()
    {
        (await service.MayWriteToAsync(currentUserId)).Should().BeFalse();

        var guest = Mock<IIdentity>();
        guest.User.Returns(AuthenticatedUser.Guest);
        identityProvider.Current.Returns(guest);

        (await service.MayWriteToAsync(Guid.NewGuid())).Should().BeFalse();
    }

    private Chat Conversation(ChatType type, params Guid[] others) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Participants = others.Prepend(currentUserId).Select(id => new GeneralUser { UserId = id }).ToArray()
    };

    private void BlocksTheWriter(Guid ownerId) =>
        userBlacklistChecker
            .GetBlockedUserIdsIfFlagEnabledAsync(
                ownerId, UserBlacklistSettings.BlockDirectMessages, Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { currentUserId });

    private async Task<bool> IsTakenAsync(Chat chat)
    {
        chatService.GetAsync(chat.Id).Returns(chat);
        factory.Create(Arg.Any<CreateMessage>(), Arg.Any<Guid>())
            .Returns(new CreateMessageEntity { MessageId = Guid.NewGuid() });
        try
        {
            await service.CreateAsync(new CreateMessage { ChatId = chat.Id, Text = "Привет" });
            return true;
        }
        catch (HttpException e) when (e.StatusCode == HttpStatusCode.Forbidden)
        {
            return false;
        }
    }
}
