using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Domain.Forum.Features.Comments;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <inheritdoc />
internal class TopicCommentRepository : ITopicCommentRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TopicCommentRepository(
        DmDbContext dbContext,
        IMapper mapper,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Task<int> Count(Guid topicId, IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var query = _dbContext.Comments
            .TagWith("DM.TopicComments.Count")
            .Where(c => !c.IsRemoved && c.EntityId == topicId);

        if (excludeUserIds is { Count: > 0 })
        {
            query = query.Where(c => !excludeUserIds.Contains(c.AuthorId));
        }

        return query.CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Comment>> Get(Guid topicId, PagingData paging, IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        var query = _dbContext.Comments
            .TagWith("DM.TopicComments.List")
            .Where(c => !c.IsRemoved && c.EntityId == topicId);

        if (excludeUserIds is { Count: > 0 })
        {
            query = query.Where(c => !excludeUserIds.Contains(c.AuthorId));
        }

        return await query
            .OrderBy(c => c.CreatedUtc)
            .Page(paging)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public Task<Comment?> Get(Guid commentId)
    {
        return _dbContext.Comments
            .TagWith("DM.TopicComments.Get")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<Comment> Create(CreateTopicCommentEntity createComment)
    {
        var commentId = _guidFactory.Create();
        var now = _dateTimeProvider.Now;

        var dbComment = new Entities.CrossDomain.Comment
        {
            CommentId = commentId,
            EntityId = createComment.TopicId,
            AuthorId = createComment.AuthorId,
            Text = createComment.Text,
            CreatedUtc = now,
            IsRemoved = false
        };

        _dbContext.Comments.Add(dbComment);

        // Update topic comment count and last comment ID
        var topic = await _dbContext.Topics.FindAsync(createComment.TopicId);
        if (topic != null)
        {
            topic.CommentCount = createComment.NewCommentCount;
            topic.LastCommentId = commentId;
        }

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Comments
            .TagWith("DM.TopicComments.Created")
            .Where(c => c.CommentId == commentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<Comment> Update(UpdateTopicCommentEntity updateComment)
    {
        var dbComment = await _dbContext.Comments.FindAsync(updateComment.CommentId);
        if (dbComment != null)
        {
            dbComment.Text = updateComment.Text;
            dbComment.ModifiedUtc = updateComment.LastUpdateUtc;
            await _dbContext.SaveChangesAsync();
        }

        return await _dbContext.Comments
            .TagWith("DM.TopicComments.Updated")
            .Where(c => c.CommentId == updateComment.CommentId)
            .ProjectTo<Comment>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task<TopicCommentToDelete?> GetForDelete(Guid commentId)
    {
        var comment = await _dbContext.Comments
            .TagWith("DM.TopicComments.GetForDelete")
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectTo<TopicCommentToDelete>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();

        if (comment == null) return null;

        // Get topic info: comment count and last comment id
        var topicInfo = await _dbContext.Topics
            .Where(t => t.TopicId == comment.EntityId)
            .Select(t => new { t.CommentCount, t.LastCommentId })
            .FirstOrDefaultAsync();

        if (topicInfo != null)
        {
            comment.TopicCommentCount = topicInfo.CommentCount;
            comment.IsLastComment = topicInfo.LastCommentId == commentId;
        }

        return comment;
    }

    /// <inheritdoc />
    public async Task<Guid?> GetSecondLastCommentId(Guid topicId)
    {
        return await _dbContext.Comments
            .TagWith("DM.TopicComments.SecondLastCommentId")
            .Where(c => !c.IsRemoved && c.EntityId == topicId)
            .OrderByDescending(c => c.CreatedUtc)
            .Skip(1)
            .Select(c => (Guid?)c.CommentId)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task Delete(DeleteTopicCommentEntity deleteComment)
    {
        var dbComment = await _dbContext.Comments.FindAsync(deleteComment.CommentId);
        if (dbComment != null)
        {
            dbComment.IsRemoved = true;
        }

        // Update topic comment count and last comment ID
        var topic = await _dbContext.Topics.FindAsync(deleteComment.TopicId);
        if (topic != null)
        {
            topic.CommentCount = deleteComment.NewCommentCount;
            topic.LastCommentId = deleteComment.NewLastCommentId;
        }

        await _dbContext.SaveChangesAsync();
    }
}
