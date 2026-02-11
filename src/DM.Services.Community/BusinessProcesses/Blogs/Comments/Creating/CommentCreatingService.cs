using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.Commentaries;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Common.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using PublicationDal = DM.Services.DataAccess.BusinessObjects.Blogs.Publication;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Community.BusinessProcesses.Blogs;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using Comment = DM.Services.Common.Dto.Comment;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Creating;

/// <inheritdoc />
internal class CommentCreatingService : ICommentCreatingService
{
    private readonly IValidator<CreateComment> _validator;
    private readonly IBlogService _blogService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ICommentaryFactory _commentaryFactory;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ICommentCreatingRepository _repository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IInvokedEventProducer _invokedEventProducer;

    /// <inheritdoc />
    public CommentCreatingService(
        IValidator<CreateComment> validator,
        IBlogService blogService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ICommentaryFactory commentaryFactory,
        IUpdateBuilderFactory updateBuilderFactory,
        ICommentCreatingRepository repository,
        IUnreadCountersRepository countersRepository,
        IInvokedEventProducer invokedEventProducer)
    {
        _validator = validator;
        _blogService = blogService;
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

        var publication = await _blogService.GetPublication(createComment.EntityId);
        _intentionManager.ThrowIfForbidden(PublicationIntention.CreateComment, publication);

        var comment = _commentaryFactory.Create(createComment, _identityProvider.Current.User.UserId);
        var publicationUpdate = _updateBuilderFactory.Create<PublicationDal>(publication.Id)
            .Field(p => p.CommentCount, publication.CommentCount + 1);
        var createdComment = await _repository.Create(comment, publicationUpdate);
        await _countersRepository.Increment(publication.Id, UnreadEntryType.Message);
        await _invokedEventProducer.Send(EventType.NewBlogComment, comment.CommentId);

        return createdComment;
    }
}
