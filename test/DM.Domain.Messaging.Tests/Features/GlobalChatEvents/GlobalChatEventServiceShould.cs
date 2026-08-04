using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Testing;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.GlobalChatEvents;

public class GlobalChatEventServiceShould : UnitTestBase
{
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IGlobalChatEventRepository> _repository;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly ISetup<IGlobalChatEventFactory, CreateGlobalChatEventEntity> _createEventSetup;
    private readonly ISetup<IGlobalChatEventFactory, CreateGlobalChatEventParticipantEntity> _createParticipantSetup;
    private readonly GlobalChatEventService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public GlobalChatEventServiceShould()
    {
        var createValidator = Mock<IValidator<CreateGlobalChatEvent>>();
        createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateGlobalChatEvent>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GlobalChatEventIntention>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GlobalChatEventIntention>(), It.IsAny<GlobalChatEvent>()));

        var factory = Mock<IGlobalChatEventFactory>();
        _createEventSetup = factory.Setup(f => f.Create(It.IsAny<CreateGlobalChatEvent>(), It.IsAny<Guid>()));
        _createParticipantSetup = factory.Setup(f => f.CreateParticipant(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()));

        _repository = Mock<IGlobalChatEventRepository>();
        _repository.Setup(r => r.Create(It.IsAny<CreateGlobalChatEventEntity>(), It.IsAny<CreateGlobalChatEventParticipantEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlobalChatEvent { Id = Guid.NewGuid() });

        var identityProvider = Mock<IIdentityProvider>();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = _currentUserId, Role = UserRole.RegularUser },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        identityProvider.Setup(p => p.Current).Returns(identity);

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);

        _eventProducer = Mock<IEventProducer>();
        _eventProducer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        _service = new GlobalChatEventService(
            createValidator.Object,
            _intentionManager.Object,
            factory.Object,
            _repository.Object,
            identityProvider.Object,
            _dateTimeProvider.Object,
            _eventProducer.Object);
    }

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var createEvent = new CreateGlobalChatEvent { Title = "Test Event" };
        var eventEntity = new CreateGlobalChatEventEntity { GlobalChatEventId = Guid.NewGuid() };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        _createEventSetup.Returns(eventEntity);
        _createParticipantSetup.Returns(participantEntity);

        await _service.CreateAsync(createEvent);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GlobalChatEventIntention.Create), Times.Once);
    }

    [Fact]
    public async Task CreateEventWithOrganizerAsParticipant()
    {
        var createEvent = new CreateGlobalChatEvent { Title = "Test Event" };
        var eventEntity = new CreateGlobalChatEventEntity { GlobalChatEventId = Guid.NewGuid() };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        _createEventSetup.Returns(eventEntity);
        _createParticipantSetup.Returns(participantEntity);

        await _service.CreateAsync(createEvent);

        _repository.Verify(r => r.Create(eventEntity, participantEntity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthorizeStartAction()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        _repository.Setup(r => r.Get(eventId)).ReturnsAsync(chatEvent);
        _repository.Setup(r => r.HasActiveEvent()).ReturnsAsync(false);
        _repository.Setup(r => r.UpdateStatus(It.IsAny<Guid>(), It.IsAny<GlobalChatEventStatus>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatEvent);

        await _service.StartAsync(eventId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GlobalChatEventIntention.Start, chatEvent), Times.Once);
    }

    [Fact]
    public async Task PublishEventStartedEvent()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        _repository.Setup(r => r.Get(eventId)).ReturnsAsync(chatEvent);
        _repository.Setup(r => r.HasActiveEvent()).ReturnsAsync(false);
        _repository.Setup(r => r.UpdateStatus(It.IsAny<Guid>(), It.IsAny<GlobalChatEventStatus>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatEvent);

        await _service.StartAsync(eventId);

        _eventProducer.Verify(p => p.SendAsync(EventType.GlobalChatEventStarted, eventId), Times.Once);
    }

    /// <summary>
    /// The actual start is a fact of its own. It is manual and does not have to
    /// fall on the planned one, so an event begun late is exactly the case where
    /// the plan plus the duration answers with an end already in the past, and a
    /// client holding only those two shows a countdown that has run out while the
    /// event is still going.
    /// </summary>
    [Fact]
    public async Task StampTheActualStartWhenTheEventGoesLive()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        _repository.Setup(r => r.Get(eventId)).ReturnsAsync(chatEvent);
        _repository.Setup(r => r.HasActiveEvent()).ReturnsAsync(false);
        _repository.Setup(r => r.UpdateStatus(It.IsAny<Guid>(), It.IsAny<GlobalChatEventStatus>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatEvent);

        await _service.StartAsync(eventId);

        _repository.Verify(r => r.UpdateStatus(
            eventId, GlobalChatEventStatus.Live, _now, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The end is stamped and the start is left alone: an event is started once,
    /// and writing the closing moment over both marks would erase how long it ran.
    /// </summary>
    [Fact]
    public async Task StampTheEndWhenTheEventIsClosed()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Live };
        _repository.Setup(r => r.Get(eventId)).ReturnsAsync(chatEvent);
        _repository.Setup(r => r.UpdateStatus(It.IsAny<Guid>(), It.IsAny<GlobalChatEventStatus>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatEvent);

        await _service.EndAsync(eventId);

        _repository.Verify(r => r.UpdateStatus(
            eventId, GlobalChatEventStatus.Ended, null, _now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthorizeJoinAction()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        _repository.Setup(r => r.Get(eventId)).ReturnsAsync(chatEvent);
        _repository.Setup(r => r.IsParticipant(eventId, _currentUserId)).ReturnsAsync(false);
        _createParticipantSetup.Returns(participantEntity);
        _repository.Setup(r => r.AddParticipant(It.IsAny<CreateGlobalChatEventParticipantEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlobalChatEventParticipant());

        await _service.JoinAsync(eventId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GlobalChatEventIntention.Join, chatEvent), Times.Once);
    }

    [Fact]
    public async Task PublishParticipantJoinedEvent()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        _repository.Setup(r => r.Get(eventId)).ReturnsAsync(chatEvent);
        _repository.Setup(r => r.IsParticipant(eventId, _currentUserId)).ReturnsAsync(false);
        _createParticipantSetup.Returns(participantEntity);
        _repository.Setup(r => r.AddParticipant(It.IsAny<CreateGlobalChatEventParticipantEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GlobalChatEventParticipant());

        await _service.JoinAsync(eventId);

        _eventProducer.Verify(p => p.SendAsync(EventType.GlobalChatEventParticipantJoined, eventId), Times.Once);
    }
}
