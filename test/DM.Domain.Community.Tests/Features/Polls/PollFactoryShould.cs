using System;
using System.Linq;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Abstractions;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Polls;

public class PollFactoryShould : UnitTestBase
{
    private readonly PollFactory _factory;
    private readonly IGuidFactory _guidFactory;

    public PollFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();

        _factory = new PollFactory(_guidFactory);
    }

    [Fact]
    public void CreatePollWithValidProperties()
    {
        var pollId = Guid.NewGuid();
        var startDate = DateTimeOffset.UtcNow;
        var endDate = DateTimeOffset.UtcNow.AddDays(7);
        var createPoll = new CreatePoll
        {
            Title = "Test Poll",
            StartsUtc = startDate,
            EndsUtc = endDate,
            Options = new[] { "Option 1", "Option 2", "Option 3" }
        };

        _guidFactory.Create().Returns(pollId);

        var result = _factory.Create(createPoll);

        result.Should().NotBeNull();
        result.Id.Should().Be(pollId);
        result.Title.Should().Be(createPoll.Title);
        result.StartsUtc.Should().Be(startDate.UtcDateTime);
        result.EndsUtc.Should().Be(endDate.UtcDateTime);
    }

    [Fact]
    public void CreatePollOptionsWithUniqueIds()
    {
        var optionIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var currentIndex = 0;

        _guidFactory.Create()
            .Returns(_ => currentIndex < optionIds.Length ? optionIds[currentIndex++] : Guid.NewGuid());

        var createPoll = new CreatePoll
        {
            Title = "Test Poll",
            StartsUtc = DateTimeOffset.UtcNow,
            EndsUtc = DateTimeOffset.UtcNow.AddDays(7),
            Options = new[] { "Option 1", "Option 2", "Option 3" }
        };

        var result = _factory.Create(createPoll);

        result.Options.Should().HaveCount(3);
        result.Options.Select(o => o.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void PreservePollOptionTexts()
    {
        _guidFactory.Create().Returns(Guid.NewGuid());

        var options = new[] { "First Option", "Second Option", "Third Option" };
        var createPoll = new CreatePoll
        {
            Title = "Test Poll",
            StartsUtc = DateTimeOffset.UtcNow,
            EndsUtc = DateTimeOffset.UtcNow.AddDays(7),
            Options = options
        };

        var result = _factory.Create(createPoll);

        result.Options.Select(o => o.Text).Should().BeEquivalentTo(options);
    }

    [Fact]
    public void PreserveDetailsWhenProvided()
    {
        _guidFactory.Create().Returns(Guid.NewGuid());

        var createPoll = new CreatePoll
        {
            Title = "Test Poll",
            Details = "Test description",
            StartsUtc = DateTimeOffset.UtcNow,
            EndsUtc = DateTimeOffset.UtcNow.AddDays(7),
            Options = new[] { "Yes", "No" }
        };

        var result = _factory.Create(createPoll);

        result.Details.Should().Be("Test description");
    }
}
