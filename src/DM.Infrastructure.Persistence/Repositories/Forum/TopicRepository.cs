using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Domain.Forum.Features.Topics;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <summary>
/// Repository for forum topics (unified CRUD operations)
/// </summary>
internal class TopicRepository : ITopicRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TopicRepository(
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

    private static readonly Guid NewsBoardId = Guid.Parse("00000000-0000-0000-0000-000000000008");
    private static readonly Guid ErrorsBoardId = Guid.Parse("00000000-0000-0000-0000-000000000006");

    // --- READ ---

    /// <inheritdoc />
    public Task<int> Count(Guid boardId, CancellationToken ct = default) => _dbContext.Topics
        .TagWith("DM.Forum.TopicsCount")
        .CountAsync(t => !t.IsRemoved && t.BoardId == boardId && !t.IsAttached, ct);

    /// <inheritdoc />
    public async Task<IEnumerable<Topic>> Get(Guid boardId, PagingData? pagingData, bool attached, CancellationToken ct = default)
    {
        var query = _dbContext.Topics
            .TagWith("DM.Forum.TopicsList")
            .Where(t => !t.IsRemoved && t.BoardId == boardId && t.IsAttached == attached)
            .ProjectTo<Topic>(_mapper.ConfigurationProvider);

        IOrderedQueryable<Topic> orderedQuery;
        if (boardId == NewsBoardId || attached)
        {
            orderedQuery = query.OrderByDescending(q => q.CreatedUtc);
        }
        else if (boardId == ErrorsBoardId)
        {
            orderedQuery = query.OrderBy(q => q.IsClosed).ThenByDescending(q => q.LastActivityUtc);
        }
        else
        {
            orderedQuery = query.OrderByDescending(q => q.LastActivityUtc);
        }

        return await orderedQuery.Page(pagingData).ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Topic?> Get(Guid topicId, BoardAccessPolicy accessPolicy, CancellationToken ct = default)
    {
        return await _dbContext.Topics
            .TagWith("DM.Forum.Topic")
            .Where(t => !t.IsRemoved && t.TopicId == topicId &&
                        (t.Board.ViewPolicy & accessPolicy) != BoardAccessPolicy.None)
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    // --- WRITE ---

    /// <inheritdoc />
    public async Task<Topic> Create(CreateTopicEntity createTopic, Guid authorId, Guid boardId, CancellationToken ct = default)
    {
        var topicId = _guidFactory.Create();
        var now = _dateTimeProvider.Now;

        var topic = new Entities.Forum.Topic
        {
            TopicId = topicId,
            BoardId = boardId,
            AuthorId = authorId,
            Title = createTopic.Title.Trim(),
            Text = createTopic.Text.Trim(),
            CreatedUtc = now,
            IsRemoved = false,
            IsClosed = false,
            IsAttached = false,
            CommentCount = 0
        };

        _dbContext.Topics.Add(topic);
        await _dbContext.SaveChangesAsync(ct);

        return await _dbContext.Topics
            .TagWith("DM.Forum.CreatedTopic")
            .Where(t => t.TopicId == topicId)
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Topic> Update(UpdateTopicEntity updateTopic, Guid? boardId = null)
    {
        var topic = await _dbContext.Topics.FindAsync(updateTopic.TopicId);
        if (topic != null)
        {
            if (!string.IsNullOrEmpty(updateTopic.Title))
            {
                topic.Title = updateTopic.Title.Trim();
            }

            if (!string.IsNullOrEmpty(updateTopic.Text))
            {
                topic.Text = updateTopic.Text.Trim();
            }

            if (updateTopic.IsClosed.HasValue)
            {
                topic.IsClosed = updateTopic.IsClosed.Value;
            }

            if (updateTopic.IsAttached.HasValue)
            {
                topic.IsAttached = updateTopic.IsAttached.Value;
            }

            if (boardId.HasValue)
            {
                topic.BoardId = boardId.Value;
            }

            await _dbContext.SaveChangesAsync();
        }

        return await _dbContext.Topics
            .TagWith("DM.Forum.UpdatedTopic")
            .Where(t => t.TopicId == updateTopic.TopicId)
            .ProjectTo<Topic>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid topicId)
    {
        var topic = await _dbContext.Topics.FindAsync(topicId);
        if (topic != null)
        {
            topic.IsRemoved = true;
            await _dbContext.SaveChangesAsync();
        }
    }
}
