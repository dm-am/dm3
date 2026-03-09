using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Identity;
using DM.Domain.Core.Identity;
using DM.Domain.Community.Authorization;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Polls;

public class PollServiceShould : UnitTestBase
{
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly ISetup<IPollFactory, CreatePollEntity> _createPollSetup;
    private readonly Mock<IPollRepository> _repository;
    private readonly ISetup<IPollRepository, Task<Poll>> _savePollSetup;
    private readonly Mock<IInvokedEventProducer> _producer;
    private readonly PollService _service;

    public PollServiceShould()
    {
        var validator = Mock<IValidator<CreatePoll>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreatePoll>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<PollIntention>()));

        var factory = Mock<IPollFactory>();
        _createPollSetup = factory.Setup(f => f.Create(It.IsAny<CreatePoll>()));

        _repository = Mock<IPollRepository>();
        _savePollSetup = _repository.Setup(r => r.Create(It.IsAny<CreatePollEntity>()));

        _producer = Mock<IInvokedEventProducer>();
        _producer.Setup(p => p.Send(It.IsAny<EventType>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Setup(p => p.Current).Returns(Identity.Guest());

        _service = new PollService(
            validator.Object,
            _intentionManager.Object,
            factory.Object,
            _repository.Object,
            _producer.Object,
            dateTimeProvider.Object,
            identityProvider.Object);
    }

    [Fact]
    public async Task AuthorizeCreatePollAction()
    {
        var createPoll = new CreatePoll { Title = "Test Poll" };
        var pollEntity = new CreatePollEntity();
        _createPollSetup.Returns(pollEntity);
        _savePollSetup.ReturnsAsync(new Poll());

        await _service.CreateAsync(createPoll);

        _intentionManager.Verify(m => m.ThrowIfForbidden(PollIntention.Create));
    }

    [Fact]
    public async Task SavePoll()
    {
        var createPoll = new CreatePoll { Title = "Test Poll" };
        var pollEntity = new CreatePollEntity();
        _createPollSetup.Returns(pollEntity);
        var expected = new Poll();
        _savePollSetup.ReturnsAsync(expected);

        var actual = await _service.CreateAsync(createPoll);

        actual.Should().Be(expected);
        _repository.Verify(r => r.Create(pollEntity), Times.Once);
    }

    [Fact]
    public async Task PublishNewPollEvent()
    {
        var createPoll = new CreatePoll { Title = "Test Poll" };
        var pollId = Guid.NewGuid();
        var pollEntity = new CreatePollEntity();
        _createPollSetup.Returns(pollEntity);
        _savePollSetup.ReturnsAsync(new Poll { Id = pollId });

        await _service.CreateAsync(createPoll);

        _producer.Verify(p => p.Send(EventType.NewPoll, pollId), Times.Once);
    }

    [Fact]
    public async Task AuthorizeVoteAction()
    {
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var poll = new Poll { Id = pollId };
        _repository.Setup(r => r.Get(pollId)).ReturnsAsync(poll);
        _repository.Setup(r => r.Vote(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(poll);

        (Poll, Guid)? capturedArg = null;
        _intentionManager
            .Setup(m => m.ThrowIfForbidden(It.IsAny<PollIntention>(), It.IsAny<(Poll, Guid)>()))
            .Callback<PollIntention, (Poll, Guid)>((_, arg) => capturedArg = arg);

        await _service.VoteAsync(pollId, optionId);

        capturedArg.Should().NotBeNull();
        capturedArg!.Value.Item1.Should().Be(poll);
        capturedArg.Value.Item2.Should().Be(optionId);
    }
}
