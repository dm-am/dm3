using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using AwesomeAssertions;
using DM.Testing;
using DM.Testing.Dsl;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.Core;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.GlobalChatEvents;

public class GlobalChatEventServiceShould : UnitTestBase
{
    private readonly IIntentionManager intentionManager;
    private readonly IGlobalChatEventRepository repository;
    private readonly IEventProducer eventProducer;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IGlobalChatEventFactory factory;
    private readonly GlobalChatEventService service;
    private readonly Guid currentUserId = Guid.NewGuid();
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

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

        intentionManager = Mock<IIntentionManager>();

        factory = Mock<IGlobalChatEventFactory>();

        repository = Mock<IGlobalChatEventRepository>();
        repository.Create(Arg.Any<CreateGlobalChatEventEntity>(), Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GlobalChatEvent { Id = Guid.NewGuid() });

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId));

        dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(now);

        eventProducer = Mock<IEventProducer>();
        eventProducer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        service = new GlobalChatEventService(
            createValidator,
            updateValidator,
            intentionManager,
            factory,
            repository,
            identityProvider,
            dateTimeProvider,
            eventProducer);
    }

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var createEvent = new CreateGlobalChatEvent { Title = "Test Event" };
        var eventEntity = new CreateGlobalChatEventEntity { GlobalChatEventId = Guid.NewGuid() };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        factory.Create(Arg.Any<CreateGlobalChatEvent>(), Arg.Any<Guid>()).Returns(eventEntity);
        factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>()).Returns(participantEntity);

        await service.CreateAsync(createEvent);

        intentionManager.Received(1).ThrowIfForbidden(GlobalChatEventIntention.Create);
    }

    [Fact]
    public async Task CreateEventWithOrganizerAsParticipant()
    {
        var createEvent = new CreateGlobalChatEvent { Title = "Test Event" };
        var eventEntity = new CreateGlobalChatEventEntity { GlobalChatEventId = Guid.NewGuid() };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        factory.Create(Arg.Any<CreateGlobalChatEvent>(), Arg.Any<Guid>()).Returns(eventEntity);
        factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>()).Returns(participantEntity);

        await service.CreateAsync(createEvent);

        await repository.Received(1).Create(eventEntity, participantEntity, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizeStartAction()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = EventIn(eventId, GlobalChatEventStatus.Scheduled);

        await service.StartAsync(eventId);

        intentionManager.Received(1).ThrowIfForbidden(GlobalChatEventIntention.Start, chatEvent);
    }

    [Fact]
    public async Task PublishEventStartedEvent()
    {
        var eventId = Guid.NewGuid();
        EventIn(eventId, GlobalChatEventStatus.Scheduled);

        await service.StartAsync(eventId);

        await eventProducer.Received(1).SendAsync(EventType.GlobalChatEventStarted, eventId);
    }

    /// <summary>
    /// The actual start is a fact of its own: an event begun late would otherwise show an end already past.
    /// </summary>
    [Fact]
    public async Task StampTheActualStartWhenTheEventGoesLive()
    {
        var eventId = Guid.NewGuid();
        EventIn(eventId, GlobalChatEventStatus.Scheduled);

        await service.StartAsync(eventId);

        await repository.Received(1).UpdateStatus(
            eventId, GlobalChatEventStatus.Scheduled, GlobalChatEventStatus.Live,
            now, null, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The end is stamped and the start is left alone: an event is started once,
    /// and writing the closing moment over both marks would erase how long it ran.
    /// </summary>
    [Fact]
    public async Task StampTheEndWhenTheEventIsClosed()
    {
        var eventId = Guid.NewGuid();
        EventIn(eventId, GlobalChatEventStatus.Live);

        await service.EndAsync(eventId);

        await repository.Received(1).UpdateStatus(
            eventId, GlobalChatEventStatus.Live, GlobalChatEventStatus.Ended,
            null, now, Arg.Any<CancellationToken>());
    }

    // Two clicks on "start" reach the conditional write together, and only one moves a row.
    [Fact]
    public async Task StaySilentWhenTheEventWasStartedBesideThisStart()
    {
        var eventId = Guid.NewGuid();
        EventIn(eventId, GlobalChatEventStatus.Scheduled);
        StatusWriteAnswers(_ => null);

        var start = () => service.StartAsync(eventId);

        await start.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
        await eventProducer.DidNotReceive().SendAsync(EventType.GlobalChatEventStarted, eventId);
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
        EventIn(eventId, GlobalChatEventStatus.Scheduled);
        StatusWriteAnswers(_ => throw new DuplicateEntityException("live"));

        var start = () => service.StartAsync(eventId);

        await start.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
        await eventProducer.DidNotReceive().SendAsync(EventType.GlobalChatEventStarted, eventId);
    }

    [Fact]
    public async Task StaySilentWhenTheEventWasEndedBesideThisEnd()
    {
        var eventId = Guid.NewGuid();
        EventIn(eventId, GlobalChatEventStatus.Live);
        StatusWriteAnswers(_ => null);

        var end = () => service.EndAsync(eventId);

        await end.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
        await eventProducer.DidNotReceive().SendAsync(EventType.GlobalChatEventEnded, eventId);
    }

    // The insert is ON CONFLICT DO NOTHING, so of two clicks only one writes.
    [Fact]
    public async Task RefuseTheJoinWhoseRowWasTakenBesideIt()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        repository.Get(eventId).Returns(chatEvent);
        repository.IsParticipant(eventId, currentUserId).Returns(false);
        factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>())
            .Returns(new CreateGlobalChatEventParticipantEntity());
        repository.AddParticipant(Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>())
            .Returns((GlobalChatEventParticipant?)null);

        var join = () => service.JoinAsync(eventId);

        await join.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict &&
                e.Message == RefusalMessage.AlreadyParticipant);
        await eventProducer.DidNotReceive()
            .SendAsync(EventType.GlobalChatEventParticipantJoined, eventId);
    }

    [Fact]
    public async Task AuthorizeJoinAction()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        repository.Get(eventId).Returns(chatEvent);
        repository.IsParticipant(eventId, currentUserId).Returns(false);
        factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>()).Returns(participantEntity);
        repository.AddParticipant(Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GlobalChatEventParticipant());

        await service.JoinAsync(eventId);

        intentionManager.Received(1).ThrowIfForbidden(GlobalChatEventIntention.Join, chatEvent);
    }

    [Fact]
    public async Task PublishParticipantJoinedEvent()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        var participantEntity = new CreateGlobalChatEventParticipantEntity();
        repository.Get(eventId).Returns(chatEvent);
        repository.IsParticipant(eventId, currentUserId).Returns(false);
        factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>()).Returns(participantEntity);
        repository.AddParticipant(Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GlobalChatEventParticipant());

        await service.JoinAsync(eventId);

        await eventProducer.Received(1).SendAsync(EventType.GlobalChatEventParticipantJoined, eventId);
    }

    // A join and an organizer's add are one write behind two doors, and the sentence keeps their
    // readers apart.
    [Fact]
    public async Task AnswerTheSecondJoinInTheSecondPerson()
    {
        var eventId = Guid.NewGuid();
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = GlobalChatEventStatus.Scheduled };
        repository.Get(eventId).Returns(chatEvent);
        repository.IsParticipant(eventId, currentUserId).Returns(true);

        var join = () => service.JoinAsync(eventId);

        await join.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict &&
                e.Message == RefusalMessage.AlreadyParticipant);
        await repository.DidNotReceive()
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
        repository.Get(eventId).Returns(chatEvent);
        repository.IsParticipant(eventId, otherUserId).Returns(true);

        var add = () => service.AddParticipantAsync(eventId, otherUserId);

        await add.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict &&
                e.Message == RefusalMessage.UserAlreadyParticipant);
        await repository.DidNotReceive()
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
        repository.Get(eventId).Returns(chatEvent);
        repository.IsParticipant(eventId, otherUserId).Returns(false);
        factory.CreateParticipant(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<bool>())
            .Returns(new CreateGlobalChatEventParticipantEntity());
        repository.AddParticipant(Arg.Any<CreateGlobalChatEventParticipantEntity>(), Arg.Any<CancellationToken>())
            .Returns((GlobalChatEventParticipant?)null);

        var add = () => service.AddParticipantAsync(eventId, otherUserId);

        await add.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict &&
                e.Message == RefusalMessage.UserAlreadyParticipant);
        await eventProducer.DidNotReceive()
            .SendAsync(EventType.GlobalChatEventParticipantJoined, eventId);
    }

    private GlobalChatEvent EventIn(Guid eventId, GlobalChatEventStatus status)
    {
        var chatEvent = new GlobalChatEvent { Id = eventId, Status = status };
        repository.Get(eventId).Returns(chatEvent);
        StatusWriteAnswers(_ => chatEvent);
        return chatEvent;
    }

    private void StatusWriteAnswers(Func<CallInfo, GlobalChatEvent?> answer) =>
        repository.UpdateStatus(
                Arg.Any<Guid>(), Arg.Any<GlobalChatEventStatus>(), Arg.Any<GlobalChatEventStatus>(),
                Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(answer);
}
