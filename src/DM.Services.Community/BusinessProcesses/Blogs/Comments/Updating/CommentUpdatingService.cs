using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Common.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Community.BusinessProcesses.Blogs.Comments.Reading;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using Comment = DM.Services.DataAccess.BusinessObjects.Common.Comment;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Updating;

/// <inheritdoc />
internal class CommentUpdatingService : ICommentUpdatingService
{
    private readonly IValidator<UpdateComment> _validator;
    private readonly ICommentReadingService _commentReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ICommentUpdatingRepository _repository;
    private readonly IInvokedEventProducer _invokedEventProducer;

    /// <inheritdoc />
    public CommentUpdatingService(
        IValidator<UpdateComment> validator,
        ICommentReadingService commentReadingService,
        IIntentionManager intentionManager,
        IDateTimeProvider dateTimeProvider,
        IUpdateBuilderFactory updateBuilderFactory,
        ICommentUpdatingRepository repository,
        IInvokedEventProducer invokedEventProducer)
    {
        _validator = validator;
        _commentReadingService = commentReadingService;
        _intentionManager = intentionManager;
        _dateTimeProvider = dateTimeProvider;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _invokedEventProducer = invokedEventProducer;
    }

    /// <inheritdoc />
    public async Task<Services.Common.Dto.Comment> Update(UpdateComment updateComment)
    {
        await _validator.ValidateAndThrowAsync(updateComment);
        var comment = await _commentReadingService.Get(updateComment.CommentId);

        _intentionManager.ThrowIfForbidden(CommentIntention.Edit, comment);
        var updateBuilder = _updateBuilderFactory.Create<Comment>(updateComment.CommentId)
            .MaybeField(f => f.Text, updateComment.Text?.Trim());

        if (updateBuilder.HasChanges())
        {
            updateBuilder.Field(f => f.ModifiedUtc, _dateTimeProvider.Now);
        }

        var updatedComment = await _repository.Update(updateBuilder);
        await _invokedEventProducer.Send(EventType.ChangedBlogComment, updateComment.CommentId);
        return updatedComment;
    }
}
