using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Topics;
using DM.Testing;
using DM.Testing.Dsl;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;

namespace DM.Domain.Forum.Tests.Dsl;

public abstract class TopicServiceTestBase : UnitTestBase
{
    protected readonly IBoardService boardService;
    protected readonly IIntentionManager intentionManager;
    private readonly IIdentityProvider identityProvider;
    private readonly IAccessPolicyConverter accessPolicyConverter;
    protected readonly ITopicRepository repository;
    protected readonly IGuidFactory guidFactory;
    protected readonly IUnreadCountersRepository unreadCountersRepository;
    private readonly IUserLookupService userLookupService;
    protected readonly IEventProducer eventProducer;
    protected readonly Guid currentUserId = Guid.NewGuid();
    private protected readonly TopicService service;

    protected TopicServiceTestBase()
    {
        var createValidator = Mock<IValidator<CreateTopic>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateTopic>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateTopic>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateTopic>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        boardService = Mock<IBoardService>();

        accessPolicyConverter = Mock<IAccessPolicyConverter>();
        accessPolicyConverter.Convert(Arg.Any<UserRole>()).Returns(BoardAccessPolicy.Guest);

        intentionManager = Mock<IIntentionManager>();
        intentionManager.IsAllowed(Arg.Any<BoardIntention>(), Arg.Any<Board>()).Returns(false);

        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId, UserRole.RegularUser));

        repository = Mock<ITopicRepository>();
        guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(_ => Guid.NewGuid());

        unreadCountersRepository = Mock<IUnreadCountersRepository>();
        unreadCountersRepository.CreateMarkerAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<UnreadEntryType>())
            .Returns(Task.CompletedTask);
        unreadCountersRepository.SelectByEntitiesAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<Guid[]>())
            .Returns(ci => ci.ArgAt<Guid[]>(2).ToDictionary(id => id, _ => 0));

        userLookupService = Mock<IUserLookupService>();

        eventProducer = Mock<IEventProducer>();
        eventProducer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        // Pass-through cache: always delegates to the factory so the
        // service's cacheable fast path still calls the repository in
        // tests. We don't want to test the cache here, only the
        // underlying read logic.
        var cache = Mock<ICache>();
        cache
            .GetOrCreateAsync(
                Arg.Any<object>(),
                Arg.Any<Func<Task<Topic[]>>>(),
                Arg.Any<TimeSpan>())
            .Returns(ci => ci.ArgAt<Func<Task<Topic[]>>>(1)());

        service = new TopicService(
            createValidator,
            updateValidator,
            boardService,
            accessPolicyConverter,
            intentionManager,
            identityProvider,
            repository,
            unreadCountersRepository,
            userLookupService,
            eventProducer,
            guidFactory,
            cache);
    }

    protected Topic SetupTopic(Guid topicId, Guid? authorId = null, bool isClosed = false, bool closedByAuthor = false)
    {
        var topic = new Topic
        {
            Id = topicId,
            Board = new Board { Id = Guid.NewGuid(), Title = "General" },
            Author = new GeneralUser { UserId = authorId ?? Guid.NewGuid() },
            IsClosed = isClosed,
            ClosedByAuthor = closedByAuthor
        };
        repository.Get(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        repository.Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>()).Returns(new TopicUpdateResult(topic, true));
        return topic;
    }
}
