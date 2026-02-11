using System;
using DM.Services.Core.Implementation;
using TopicDal = DM.Services.DataAccess.BusinessObjects.Boards.Topic;
using DM.Services.Forum.BusinessProcesses.Topics.Creating;
using DM.Services.Forum.Dto.Input;
using DM.Tests.Core;
using FluentAssertions;
using Moq.Language.Flow;
using Xunit;

namespace DM.Services.Forum.Tests.BusinessProcesses.Topics;

public class TopicFactoryShould : UnitTestBase
{
    private readonly ISetup<IGuidFactory, Guid> idSetup;
    private readonly ISetup<IDateTimeProvider, DateTimeOffset> currentMomentSetup;
    private readonly TopicFactory factory;

    public TopicFactoryShould()
    {
        var guidFactory = Mock<IGuidFactory>();
        idSetup = guidFactory.Setup(f => f.Create());
        var dateTimeProvider = Mock<IDateTimeProvider>();
        currentMomentSetup = dateTimeProvider.Setup(p => p.Now);
        factory = new TopicFactory(guidFactory.Object, dateTimeProvider.Object);
    }

    [Fact]
    public void CreateTopic()
    {
        var forumId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        idSetup.Returns(topicId);
        var rightNow = new DateTime(2016, 05, 12);
        currentMomentSetup.Returns(rightNow);
        var actual = factory.Create(forumId, userId, new CreateTopic
        {
            Text = "text",
            Title = "title"
        });

        actual.Should().BeEquivalentTo(new TopicDal
        {
            TopicId = topicId,
            BoardId = forumId,
            UserId = userId,
            CreatedUtc = rightNow,
            Title = "title",
            Text = "text"
        });
    }
}