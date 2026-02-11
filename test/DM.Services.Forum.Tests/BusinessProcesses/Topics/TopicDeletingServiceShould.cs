using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using TopicDal = DM.Services.DataAccess.BusinessObjects.Boards.Topic;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Forum.Authorization;
using DM.Services.Forum.BusinessProcesses.Topics.Deleting;
using DM.Services.Forum.BusinessProcesses.Topics.Reading;
using DM.Services.Forum.BusinessProcesses.Topics.Updating;
using TopicDto = DM.Services.Forum.Dto.Output.Topic;
using DM.Services.MessageQueuing.GeneralBus;
using DM.Tests.Core;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace DM.Services.Forum.Tests.BusinessProcesses.Topics;

public class TopicDeletingServiceShould : UnitTestBase
{
    private readonly ISetup<ITopicReadingService, Task<TopicDto>> getTopicSetup;
    private readonly Mock<IIntentionManager> intentionManager;
    private readonly Mock<IUpdateBuilder<TopicDal>> updateBuilder;
    private readonly Mock<ITopicUpdatingRepository> updatingRepository;
    private readonly ISetup<ITopicUpdatingRepository, Task<TopicDto>> updateSetup;
    private readonly Mock<IInvokedEventProducer> publisher;
    private readonly Mock<IUnreadCountersRepository> unreadCountersRepository;
    private readonly TopicDeletingService service;

    public TopicDeletingServiceShould()
    {
        var readingService = Mock<ITopicReadingService>();
        getTopicSetup = readingService.Setup(s => s.GetTopic(It.IsAny<Guid>(), It.IsAny<CancellationToken>()));

        intentionManager = Mock<IIntentionManager>();
        intentionManager
            .Setup(m => m.ThrowIfForbidden(It.IsAny<ForumIntention>(), It.IsAny<TopicDto>()));

        updateBuilder = Mock<IUpdateBuilder<TopicDal>>();
        updateBuilder
            .Setup(b => b.Field(t => t.IsRemoved, It.IsAny<bool>()))
            .Returns(updateBuilder.Object);
        var updateBuilderFactory = Mock<IUpdateBuilderFactory>();
        updateBuilderFactory
            .Setup(f => f.Create<TopicDal>(It.IsAny<Guid>()))
            .Returns(updateBuilder.Object);

        updatingRepository = Mock<ITopicUpdatingRepository>();
        updateSetup = updatingRepository.Setup(r => r.Update(It.IsAny<IUpdateBuilder<TopicDal>>()));

        publisher = Mock<IInvokedEventProducer>();
        publisher
            .Setup(p => p.Send(It.IsAny<EventType>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        unreadCountersRepository = Mock<IUnreadCountersRepository>();
        unreadCountersRepository
            .Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
            .Returns(Task.CompletedTask);

        service = new TopicDeletingService(readingService.Object,
            intentionManager.Object, updateBuilderFactory.Object,
            updatingRepository.Object, publisher.Object,
            unreadCountersRepository.Object);
    }

    [Fact]
    public async Task AuthorizeDeletingAction()
    {
        var topicId = Guid.NewGuid();
        var board = new Dto.Output.Board();
        var topic = new TopicDto {Board = board};
        getTopicSetup.ReturnsAsync(topic);
        updateSetup.ReturnsAsync(new TopicDto());

        await service.DeleteTopic(topicId);

        intentionManager.Verify(m => m.ThrowIfForbidden(ForumIntention.AdministrateTopics, board), Times.Once);
    }

    [Fact]
    public async Task OnlyUpdateRemovedField()
    {
        var topicId = Guid.NewGuid();
        var topic = new TopicDto();
        getTopicSetup.ReturnsAsync(topic);
        updateSetup.ReturnsAsync(new TopicDto());

        await service.DeleteTopic(topicId);

        updateBuilder.Verify(b => b.Field(t => t.IsRemoved, true), Times.Once);
        updateBuilder.VerifyNoOtherCalls();
        updatingRepository.Verify(r => r.Update(updateBuilder.Object), Times.Once);
        updatingRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteUnreadCounters()
    {
        var topicId = Guid.NewGuid();
        var topic = new TopicDto();
        getTopicSetup.ReturnsAsync(topic);
        updateSetup.ReturnsAsync(new TopicDto());

        await service.DeleteTopic(topicId);

        unreadCountersRepository.Verify(r => r.Delete(topicId, UnreadEntryType.Message), Times.Once);
        unreadCountersRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublishEvent()
    {
        var topicId = Guid.NewGuid();
        var topic = new TopicDto();
        getTopicSetup.ReturnsAsync(topic);
        updateSetup.ReturnsAsync(new TopicDto());

        await service.DeleteTopic(topicId);

        publisher.Verify(p => p.Send(EventType.DeletedForumTopic, topicId), Times.Once);
        publisher.VerifyNoOtherCalls();
    }
}