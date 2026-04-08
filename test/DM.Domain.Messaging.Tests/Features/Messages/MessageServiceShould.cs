using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Domain.Messaging.Features.Messages;
using DM.Testing;
using FluentValidation;
using FluentValidation.Results;
using DM.Domain.Account.Features.Authentication;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Messages;

public class MessageServiceShould : UnitTestBase
{
    private readonly Mock<IChatService> _chatService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IMessageRepository> _repository;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly Mock<IUnreadCountersRepository> _unreadCountersRepository;
    private readonly ISetup<IMessageFactory, CreateMessageEntity> _createMessageSetup;
    private readonly MessageService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public MessageServiceShould()
    {
        var createValidator = Mock<IValidator<CreateMessage>>();
        createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateMessage>>();
        updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _chatService = Mock<IChatService>();
        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<ChatIntention>(), It.IsAny<Chat>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<MessageIntention>(), It.IsAny<Message>()));

        var factory = Mock<IMessageFactory>();
        _createMessageSetup = factory.Setup(f => f.Create(It.IsAny<CreateMessage>(), It.IsAny<Guid>()));

        _repository = Mock<IMessageRepository>();
        _repository.Setup(r => r.Create(It.IsAny<CreateMessageEntity>(), It.IsAny<UpdateChatLastMessageEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message { Id = Guid.NewGuid() });

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _unreadCountersRepository.Setup(r => r.IncrementExcludingAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _eventProducer = Mock<IEventProducer>();
        _eventProducer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        var identityProvider = Mock<IIdentityProvider>();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = _currentUserId, Role = UserRole.RegularUser },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        identityProvider.Setup(p => p.Current).Returns(identity);

        var userBlacklistChecker = Mock<IUserBlacklistChecker>();
        userBlacklistChecker.Setup(c => c.GetBlockedUserIdsIfFlagEnabledAsync(It.IsAny<Guid>(), It.IsAny<UserBlacklistSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());

        var globalChatEventRepository = Mock<IGlobalChatEventRepository>();
        globalChatEventRepository.Setup(r => r.GetActiveEvent()).ReturnsAsync((GlobalChatEvent?)null);

        _service = new MessageService(
            _chatService.Object,
            globalChatEventRepository.Object,
            createValidator.Object,
            updateValidator.Object,
            _intentionManager.Object,
            factory.Object,
            _repository.Object,
            _unreadCountersRepository.Object,
            _eventProducer.Object,
            identityProvider.Object,
            userBlacklistChecker.Object);
    }

    [Fact]
    public async Task AuthorizeCreateMessageAction()
    {
        var chatId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat { Id = chatId, Type = ChatType.Direct, Participants = Array.Empty<GeneralUser>() };
        var messageEntity = new CreateMessageEntity { MessageId = Guid.NewGuid() };
        _chatService.Setup(s => s.GetAsync(chatId)).ReturnsAsync(chat);
        _createMessageSetup.Returns(messageEntity);

        await _service.CreateAsync(createMessage);

        _intentionManager.Verify(m => m.ThrowIfForbidden(ChatIntention.CreateMessage, chat), Times.Once);
    }

    [Fact]
    public async Task SaveMessage()
    {
        var chatId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat { Id = chatId, Type = ChatType.Direct, Participants = Array.Empty<GeneralUser>() };
        var messageEntity = new CreateMessageEntity { MessageId = Guid.NewGuid() };
        _chatService.Setup(s => s.GetAsync(chatId)).ReturnsAsync(chat);
        _createMessageSetup.Returns(messageEntity);

        await _service.CreateAsync(createMessage);

        _repository.Verify(r => r.Create(messageEntity, It.IsAny<UpdateChatLastMessageEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IncrementUnreadCountersExcludingSender()
    {
        var chatId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat { Id = chatId, Type = ChatType.Direct, Participants = Array.Empty<GeneralUser>() };
        var messageEntity = new CreateMessageEntity { MessageId = Guid.NewGuid() };
        _chatService.Setup(s => s.GetAsync(chatId)).ReturnsAsync(chat);
        _createMessageSetup.Returns(messageEntity);

        await _service.CreateAsync(createMessage);

        _unreadCountersRepository.Verify(
            r => r.IncrementExcludingAsync(chatId, UnreadEntryType.Message, _currentUserId),
            Times.Once);
    }

    [Fact]
    public async Task PublishNewMessageEvent()
    {
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var createMessage = new CreateMessage { ChatId = chatId, Text = "Hello" };
        var chat = new Chat { Id = chatId, Type = ChatType.Direct, Participants = Array.Empty<GeneralUser>() };
        var messageEntity = new CreateMessageEntity { MessageId = messageId };
        _chatService.Setup(s => s.GetAsync(chatId)).ReturnsAsync(chat);
        _createMessageSetup.Returns(messageEntity);
        _repository.Setup(r => r.Create(It.IsAny<CreateMessageEntity>(), It.IsAny<UpdateChatLastMessageEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Message { Id = messageId });

        await _service.CreateAsync(createMessage);

        _eventProducer.Verify(p => p.SendAsync(EventType.NewMessage, messageId), Times.Once);
    }

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var messageId = Guid.NewGuid();
        var message = new Message { Id = messageId };
        _repository.Setup(r => r.Get(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(message);
        _repository.Setup(r => r.Delete(messageId, _currentUserId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _service.DeleteAsync(messageId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(MessageIntention.Delete, message), Times.Once);
    }
}
