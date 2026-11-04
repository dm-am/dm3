using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Community.Authorization;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Polls;

public class PollServiceShould : UnitTestBase
{
    private readonly IIntentionManager intentionManager;
    private readonly IPollFactory factory;
    private readonly IPollRepository repository;
    private readonly IEventProducer producer;
    private readonly PollService service;

    public PollServiceShould()
    {
        var validator = Mock<IValidator<CreatePoll>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<CreatePoll>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        intentionManager = Mock<IIntentionManager>();
        factory = Mock<IPollFactory>();
        repository = Mock<IPollRepository>();
        producer = Mock<IEventProducer>();
        producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.Guest());

        service = new PollService(
            validator,
            Mock<IValidator<UpdatePoll>>(),
            intentionManager,
            factory,
            repository,
            producer,
            identityProvider);
    }

    [Fact]
    public async Task AuthorizeCreatePollAction()
    {
        var createPoll = new CreatePoll { Title = "Test Poll" };
        var pollEntity = new CreatePollEntity();
        factory.Create(Arg.Any<CreatePoll>()).Returns(pollEntity);
        repository.Create(Arg.Any<CreatePollEntity>()).Returns(new Poll());

        await service.CreateAsync(createPoll);

        intentionManager.Received(1).ThrowIfForbidden(PollIntention.Create);
    }

    [Fact]
    public async Task SavePoll()
    {
        var createPoll = new CreatePoll { Title = "Test Poll" };
        var pollEntity = new CreatePollEntity();
        factory.Create(Arg.Any<CreatePoll>()).Returns(pollEntity);
        var expected = new Poll();
        repository.Create(Arg.Any<CreatePollEntity>()).Returns(expected);

        var actual = await service.CreateAsync(createPoll);

        actual.Should().Be(expected);
        await repository.Received(1).Create(pollEntity);
    }

    [Fact]
    public async Task PublishNewPollEvent()
    {
        var createPoll = new CreatePoll { Title = "Test Poll" };
        var pollId = Guid.NewGuid();
        var pollEntity = new CreatePollEntity();
        factory.Create(Arg.Any<CreatePoll>()).Returns(pollEntity);
        repository.Create(Arg.Any<CreatePollEntity>()).Returns(new Poll { Id = pollId });

        await service.CreateAsync(createPoll);

        await producer.Received(1).SendAsync(EventType.NewPoll, pollId);
    }

    [Fact]
    public async Task AuthorizeVoteAction()
    {
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var poll = new Poll { Id = pollId };
        repository.Get(pollId).Returns(poll);
        repository.Vote(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(poll);

        await service.VoteAsync(pollId, optionId);

        intentionManager.Received(1).ThrowIfForbidden(
            PollIntention.Vote, Arg.Is<(Poll, Guid)>(t => t.Item1 == poll && t.Item2 == optionId));
    }
}
