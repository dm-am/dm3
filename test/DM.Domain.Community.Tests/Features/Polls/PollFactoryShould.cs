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
    private readonly PollFactory factory;
    private readonly IGuidFactory guidFactory;
    private readonly DateTimeOffset now = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public PollFactoryShould()
    {
        guidFactory = Mock<IGuidFactory>();
        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(now);

        factory = new PollFactory(guidFactory, dateTimeProvider);
    }

    [Fact]
    public void CreatePollWithValidProperties()
    {
        var pollId = Guid.NewGuid();
        var startDate = now.AddHours(1);
        var endDate = now.AddDays(7);
        var options = new[] { "First Option", "Second Option", "Third Option" };
        var createPoll = new CreatePoll
        {
            Title = "Test Poll",
            Details = "Test description",
            StartsUtc = startDate,
            EndsUtc = endDate,
            Options = options
        };

        guidFactory.Create().Returns(pollId);

        var result = factory.Create(createPoll);

        result.Id.Should().Be(pollId);
        result.Title.Should().Be(createPoll.Title);
        result.Details.Should().Be("Test description");
        result.StartsUtc.Should().Be(startDate.UtcDateTime);
        result.EndsUtc.Should().Be(endDate.UtcDateTime);
        result.Options.Select(o => o.Text).Should().BeEquivalentTo(options);
    }

    [Fact]
    public void CreatePollOptionsWithUniqueIds()
    {
        var optionIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var currentIndex = 0;

        guidFactory.Create()
            .Returns(_ => currentIndex < optionIds.Length ? optionIds[currentIndex++] : Guid.NewGuid());

        var createPoll = new CreatePoll
        {
            Title = "Test Poll",
            StartsUtc = DateTimeOffset.UtcNow,
            EndsUtc = DateTimeOffset.UtcNow.AddDays(7),
            Options = new[] { "Option 1", "Option 2", "Option 3" }
        };

        var result = factory.Create(createPoll);

        result.Options.Should().HaveCount(3);
        result.Options.Select(o => o.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void RecordAStartBehindTheClockAsNow()
    {
        var createPoll = new CreatePoll
        {
            Title = "Test Poll",
            StartsUtc = now.AddSeconds(-30),
            EndsUtc = now.AddDays(7),
            Options = new[] { "Option 1", "Option 2" }
        };

        var result = factory.Create(createPoll);

        result.StartsUtc.Should().Be(now.UtcDateTime);
    }
}
