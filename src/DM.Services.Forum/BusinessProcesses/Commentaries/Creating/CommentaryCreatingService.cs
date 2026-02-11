using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.Commentaries;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Common.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using TopicDal = DM.Services.DataAccess.BusinessObjects.Boards.Topic;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Forum.Authorization;
using DM.Services.Forum.BusinessProcesses.Topics.Reading;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using Comment = DM.Services.Common.Dto.Comment;

namespace DM.Services.Forum.BusinessProcesses.Commentaries.Creating;

/// <inheritdoc />
internal class CommentaryCreatingService : ICommentaryCreatingService
{
    private readonly IValidator<CreateComment> _validator;
    private readonly ITopicReadingService _topicReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ICommentaryFactory _commentaryFactory;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ICommentaryCreatingRepository _repository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IInvokedEventProducer _invokedEventProducer;

    /// <inheritdoc />
    public CommentaryCreatingService(
        IValidator<CreateComment> validator,
        ITopicReadingService topicReadingService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ICommentaryFactory commentaryFactory,
        IUpdateBuilderFactory updateBuilderFactory,
        ICommentaryCreatingRepository repository,
        IUnreadCountersRepository countersRepository,
        IInvokedEventProducer invokedEventProducer)
    {
        _validator = validator;
        _topicReadingService = topicReadingService;
        _intentionManager = intentionManager;
        _commentaryFactory = commentaryFactory;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _countersRepository = countersRepository;
        _invokedEventProducer = invokedEventProducer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<Comment> Create(CreateComment createComment)
    {
        await _validator.ValidateAndThrowAsync(createComment);

        var topic = await _topicReadingService.GetTopic(createComment.EntityId);
        _intentionManager.ThrowIfForbidden(TopicIntention.CreateComment, topic);

        var comment = _commentaryFactory.Create(createComment, _identityProvider.Current.User.UserId);
        var topicUpdate = _updateBuilderFactory.Create<TopicDal>(topic.Id)
            .Field(t => t.LastCommentId, comment.CommentId);
        var createdComment = await _repository.Create(comment, topicUpdate);
        await _countersRepository.Increment(topic.Id, UnreadEntryType.Message);
        await _invokedEventProducer.Send(EventType.NewForumComment, comment.CommentId);

        return createdComment;
    }
}