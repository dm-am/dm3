using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Identity;
using DM.Domain.Core.Identity;
using DM.Domain.Community.Authorization;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Polls;

public class PollServiceShould : UnitTestBase
{
    private readonly IIntentionManager _intentionManager;
    private readonly IPollFactory _factory;
    private readonly IPollRepository _repository;
    private readonly IEventProducer _producer;
    private readonly PollService _service;

    public PollServiceShould()
    {
        var validator = Mock<IValidator<CreatePoll>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<CreatePoll>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();
        _factory = Mock<IPollFactory>();
        _repository = Mock<IPollRepository>();
        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identity.Guest());

        var updateValidator = Mock<IValidator<UpdatePoll>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdatePoll>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _service = new PollService(
            validator,
            updateValidator,
            _intentionManager,
            _factory,
            _repository,
            _producer,
            identityProvider);
    }

    [Fact]
    public async Task AuthorizeCreatePollAction()
    {
        var createPoll = new CreatePoll { Title = "Test Poll" };
        var pollEntity = new CreatePollEntity();
        _factory.Create(Arg.Any<CreatePoll>()).Returns(pollEntity);
        _repository.Create(Arg.Any<CreatePollEntity>()).Returns(new Poll());

        await _service.CreateAsync(createPoll);

        _intentionManager.Received(1).ThrowIfForbidden(PollIntention.Create);
    }

    [Fact]
    public async Task SavePoll()
    {
        var createPoll = new CreatePoll { Title = "Test Poll" };
        var pollEntity = new CreatePollEntity();
        _factory.Create(Arg.Any<CreatePoll>()).Returns(pollEntity);
        var expected = new Poll();
        _repository.Create(Arg.Any<CreatePollEntity>()).Returns(expected);

        var actual = await _service.CreateAsync(createPoll);

        actual.Should().Be(expected);
        await _repository.Received(1).Create(pollEntity);
    }

    [Fact]
    public async Task PublishNewPollEvent()
    {
        var createPoll = new CreatePoll { Title = "Test Poll" };
        var pollId = Guid.NewGuid();
        var pollEntity = new CreatePollEntity();
        _factory.Create(Arg.Any<CreatePoll>()).Returns(pollEntity);
        _repository.Create(Arg.Any<CreatePollEntity>()).Returns(new Poll { Id = pollId });

        await _service.CreateAsync(createPoll);

        await _producer.Received(1).SendAsync(EventType.NewPoll, pollId);
    }

    [Fact]
    public async Task AuthorizeVoteAction()
    {
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var poll = new Poll { Id = pollId };
        _repository.Get(pollId).Returns(poll);
        _repository.Vote(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(poll);

        (Poll, Guid)? capturedArg = null;
        _intentionManager
            .When(m => m.ThrowIfForbidden(Arg.Any<PollIntention>(), Arg.Any<(Poll, Guid)>()))
            .Do(ci => capturedArg = ci.ArgAt<(Poll, Guid)>(1));

        await _service.VoteAsync(pollId, optionId);

        capturedArg.Should().NotBeNull();
        capturedArg!.Value.Item1.Should().Be(poll);
        capturedArg.Value.Item2.Should().Be(optionId);
    }
}
