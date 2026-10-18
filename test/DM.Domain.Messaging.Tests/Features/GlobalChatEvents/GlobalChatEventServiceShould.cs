using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Identity;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using AwesomeAssertions;
using DM.Testing;
using DM.Testing.Markup;
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
            _eventProducer,
            new TagNamesAsTyped());
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
        _repository.UpdateStatus(Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
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
        _repository.UpdateStatus(Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
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
        _repository.UpdateStatus(Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(chatEvent);

        await _service.StartAsync(eventId);

        await _repository.Received(1).UpdateStatus(
            eventId, GlobalChatEventStatus.Scheduled, GlobalChatEventStatus.Live,
            _now, null, Arg.Any<CancellationToken>());
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
        _repository.UpdateStatus(Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(chatEvent);

        await _service.EndAsync(eventId);

        await _repository.Received(1).UpdateStatus(
            eventId, GlobalChatEventStatus.Live, GlobalChatEventStatus.Ended,
            null, _now, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The start that moved nothing announces nothing.
    /// </summary>
    /// <remarks>
    /// Two clicks on "start" reach a scheduled event together and the write is
    /// conditional on that status, so only one of them moves a row. Announcing
    /// from the other tells every reader of the chat that the event began a
    /// second time, and hands the client a second live banner to raise.
    /// </remarks>
    [Fact]
    public async Task StaySilentWhenTheEventWasStartedBesideThisStart()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        _repository.Get(eventId).Returns(chatEvent);
        _repository.HasActiveEvent().Returns(false);
        _repository.UpdateStatus(Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns((GlobalChatEvent?)null);

        var start = () => _service.StartAsync(eventId);

        await start.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
        await _eventProducer.DidNotReceive().SendAsync(EventType.GlobalChatEventStarted, eventId);
    }

    /// <summary>
    /// Another event taking the live status beside this start is a refusal and
    /// not a 500: the index is the last word on "one at a time", and what it
    /// refuses is the same fact the check before the write answers with.
    /// </summary>
    [Fact]
    public async Task RefuseTheStartWhoseLiveStatusWasTakenBesideIt()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        _repository.Get(eventId).Returns(chatEvent);
        _repository.HasActiveEvent().Returns(false);
        _repository.UpdateStatus(Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns<GlobalChatEvent?>(_ => throw new DuplicateEntityException("live"));

        var start = () => _service.StartAsync(eventId);

        await start.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
        await _eventProducer.DidNotReceive().SendAsync(EventType.GlobalChatEventStarted, eventId);
    }

    /// <summary>The end that moved nothing announces nothing, for the same reason.</summary>
    [Fact]
    public async Task StaySilentWhenTheEventWasEndedBesideThisEnd()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Live };
        _repository.Get(eventId).Returns(chatEvent);
        _repository.UpdateStatus(Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns((GlobalChatEvent?)null);

        var end = () => _service.EndAsync(eventId);

        await end.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
        await _eventProducer.DidNotReceive().SendAsync(EventType.GlobalChatEventEnded, eventId);
    }

    /// <summary>
    /// The join that wrote no row is refused and announces nothing.
    /// </summary>
    /// <remarks>
    /// The pair is unique and the insert is ON CONFLICT DO NOTHING, so of two
    /// clicks meeting on one list only one writes. The other is told the state it
    /// asked for already holds, which is the same answer the read before the
    /// write gives to a second click that arrives later - and it is told so in
    /// the second person, because it was its own place it asked for.
    /// </remarks>
    [Fact]
    public async Task RefuseTheJoinWhoseRowWasTakenBesideIt()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        _repository.Get(eventId).Returns(chatEvent);
        _repository.IsParticipant(eventId, _currentUserId).Returns(false);
        _factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>())
            .Returns(new CreateGlobalChatEventParticipantEntity());
        _repository.AddParticipant(Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>())
            .Returns((GlobalChatEventParticipant?)null);

        var join = () => _service.JoinAsync(eventId);

        await join.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict &&
                e.Message == RefusalMessage.AlreadyParticipant);
        await _eventProducer.DidNotReceive()
            .SendAsync(EventType.GlobalChatEventParticipantJoined, eventId);
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

    /// <summary>
    /// The second press of "Участвовать" is answered about the reader himself.
    /// </summary>
    /// <remarks>
    /// A join and an organizer's add are one write behind two doors, and the
    /// sentence is the only thing that keeps their readers apart. A person who
    /// signed himself up and is answered "Пользователь уже участвует в эвенте"
    /// reads it as being about somebody else and presses again.
    /// </remarks>
    [Fact]
    public async Task AnswerTheSecondJoinInTheSecondPerson()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        _repository.Get(eventId).Returns(chatEvent);
        _repository.IsParticipant(eventId, _currentUserId).Returns(true);

        var join = () => _service.JoinAsync(eventId);

        await join.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict &&
                e.Message == RefusalMessage.AlreadyParticipant);
        await _repository.DidNotReceive()
            .AddParticipant(Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The organizer who adds somebody already on the list is answered about
    /// that somebody, not about the organizer.
    /// </summary>
    [Fact]
    public async Task AnswerTheOrganizerAboutTheUserTheyNamed()
    {
        var eventId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        _repository.Get(eventId).Returns(chatEvent);
        _repository.IsParticipant(eventId, otherUserId).Returns(true);

        var add = () => _service.AddParticipantAsync(eventId, otherUserId);

        await add.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict &&
                e.Message == RefusalMessage.UserAlreadyParticipant);
        await _repository.DidNotReceive()
            .AddParticipant(Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The row taken beside the organizer's add is refused in the same words as
    /// the read before it: the race and the check are one fact reached twice,
    /// and the reader of both is the organizer.
    /// </summary>
    [Fact]
    public async Task AnswerTheOrganizerWhoseRowWasTakenBesideThem()
    {
        var eventId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        _repository.Get(eventId).Returns(chatEvent);
        _repository.IsParticipant(eventId, otherUserId).Returns(false);
        _factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>())
            .Returns(new CreateGlobalChatEventParticipantEntity());
        _repository.AddParticipant(Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>())
            .Returns((GlobalChatEventParticipant?)null);

        var add = () => _service.AddParticipantAsync(eventId, otherUserId);

        await add.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict &&
                e.Message == RefusalMessage.UserAlreadyParticipant);
        await _eventProducer.DidNotReceive()
            .SendAsync(EventType.GlobalChatEventParticipantJoined, eventId);
    }
}
