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
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.GlobalChatEvents;

public class GlobalChatEventServiceShould : UnitTestBase
{
    private readonly IIntentionManager _intentionManager;
    private readonly IGlobalChatEventRepository _repository;
    private readonly IEventProducer _eventProducer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGlobalChatEventFactory _factory;
    private readonly GlobalChatEventService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public GlobalChatEventServiceShould()
    {
        var createValidator = Mock<IValidator<CreateGlobalChatEvent>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateGlobalChatEvent>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateGlobalChatEvent>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateGlobalChatEvent>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _factory = Mock<IGlobalChatEventFactory>();

        _repository = Mock<IGlobalChatEventRepository>();
        _repository.Create(Arg.Any<CreateGlobalChatEventEntity>(), Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GlobalChatEvent { Id = Guid.NewGuid() });

        var identityProvider = Mock<IIdentityProvider>();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = _currentUserId, Role = UserRole.RegularUser },
            new Session { Id = Guid.NewGuid() },
            new UserSettings(),
            "token");
        identityProvider.Current.Returns(identity);

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(_now);

        _eventProducer = Mock<IEventProducer>();
        _eventProducer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _service = new GlobalChatEventService(
            createValidator,
            updateValidator,
            _intentionManager,
            _factory,
            _repository,
            identityProvider,
            _dateTimeProvider,
            _eventProducer);
    }

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var createEvent = new CreateGlobalChatEvent { Title = "Test Event" };
        var eventEntity = new CreateGlobalChatEventEntity { GlobalChatEventId = Guid.NewGuid() };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        _factory.Create(Arg.Any<CreateGlobalChatEvent>(), Arg.Any<Guid>()).Returns(eventEntity);
        _factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>()).Returns(participantEntity);

        await _service.CreateAsync(createEvent);

        _intentionManager.Received(1).ThrowIfForbidden(GlobalChatEventIntention.Create);
    }

    [Fact]
    public async Task CreateEventWithOrganizerAsParticipant()
    {
        var createEvent = new CreateGlobalChatEvent { Title = "Test Event" };
        var eventEntity = new CreateGlobalChatEventEntity { GlobalChatEventId = Guid.NewGuid() };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        _factory.Create(Arg.Any<CreateGlobalChatEvent>(), Arg.Any<Guid>()).Returns(eventEntity);
        _factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>()).Returns(participantEntity);

        await _service.CreateAsync(createEvent);

        await _repository.Received(1).Create(eventEntity, participantEntity, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizeStartAction()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        _repository.Get(eventId).Returns(chatEvent);
        _repository.HasActiveEvent().Returns(false);
        _repository.UpdateStatus(Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(chatEvent);

        await _service.StartAsync(eventId);

        _intentionManager.Received(1).ThrowIfForbidden(GlobalChatEventIntention.Start, chatEvent);
    }

    [Fact]
    public async Task PublishEventStartedEvent()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        _repository.Get(eventId).Returns(chatEvent);
        _repository.HasActiveEvent().Returns(false);
        _repository.UpdateStatus(Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(chatEvent);

        await _service.StartAsync(eventId);

        await _eventProducer.Received(1).SendAsync(EventType.GlobalChatEventStarted, eventId);
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
        _repository.Get(eventId).Returns(chatEvent);
        _repository.HasActiveEvent().Returns(false);
        _repository.UpdateStatus(Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(chatEvent);

        await _service.StartAsync(eventId);

        await _repository.Received(1).UpdateStatus(
            eventId, GlobalChatEventStatus.Live, _now, null, Arg.Any<CancellationToken>());
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
        _repository.Get(eventId).Returns(chatEvent);
        _repository.UpdateStatus(Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(chatEvent);

        await _service.EndAsync(eventId);

        await _repository.Received(1).UpdateStatus(
            eventId, GlobalChatEventStatus.Ended, null, _now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizeJoinAction()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        _repository.Get(eventId).Returns(chatEvent);
        _repository.IsParticipant(eventId, _currentUserId).Returns(false);
        _factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>()).Returns(participantEntity);
        _repository.AddParticipant(Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GlobalChatEventParticipant());

        await _service.JoinAsync(eventId);

        _intentionManager.Received(1).ThrowIfForbidden(GlobalChatEventIntention.Join, chatEvent);
    }

    [Fact]
    public async Task PublishParticipantJoinedEvent()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        _repository.Get(eventId).Returns(chatEvent);
        _repository.IsParticipant(eventId, _currentUserId).Returns(false);
        _factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>()).Returns(participantEntity);
        _repository.AddParticipant(Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GlobalChatEventParticipant());

        await _service.JoinAsync(eventId);

        await _eventProducer.Received(1).SendAsync(EventType.GlobalChatEventParticipantJoined, eventId);
    }
}
