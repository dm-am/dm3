using System;
using DM.Services.Common.Dto;
using DM.Services.Core.Implementation;
using Comment = DM.Services.DataAccess.BusinessObjects.Common.Comment;

namespace DM.Services.Common.BusinessProcesses.Commentaries;

/// <inheritdoc />
internal class CommentaryFactory : ICommentaryFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public CommentaryFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Comment Create(CreateComment createComment, Guid userId)
    {
        return new Comment
        {
            CommentId = _guidFactory.Create(),
            EntityId = createComment.EntityId,
            UserId = userId,
            CreateDate = _dateTimeProvider.Now,
            Text = createComment.Text.Trim(),
            IsRemoved = false
        };
    }
}