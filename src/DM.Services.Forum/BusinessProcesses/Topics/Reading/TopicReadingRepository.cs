using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Extensions;
using DM.Services.DataAccess;
using DM.Services.Forum.Dto.Output;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Forum.BusinessProcesses.Topics.Reading;

/// <inheritdoc />
internal class TopicReadingRepository(
    DmDbContext dbContext,
    IMapper mapper) : ITopicReadingRepository
{
    private static readonly Guid NewsBoardId = Guid.Parse("00000000-0000-0000-0000-000000000008");
    private static readonly Guid ErrorsBoardId = Guid.Parse("00000000-0000-0000-0000-000000000006");

    /// <inheritdoc />
    public Task<int> Count(Guid boardId, CancellationToken ct = default) => dbContext.Topics
        .TagWith("DM.Forum.TopicsCount")
        .CountAsync(t => !t.IsRemoved && t.BoardId == boardId && !t.IsAttached, ct);

    /// <inheritdoc />
    public async Task<IEnumerable<Topic>> Get(Guid boardId, PagingData? pagingData, bool attached, CancellationToken ct = default)
    {
        var query = dbContext.Topics
            .TagWith("DM.Forum.TopicsList")
            .Where(t => !t.IsRemoved && t.BoardId == boardId && t.IsAttached == attached)
            .ProjectTo<Topic>(mapper.ConfigurationProvider);

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
        return await dbContext.Topics
            .TagWith("DM.Forum.Topic")
            .Where(t => !t.IsRemoved && t.TopicId == topicId &&
                        (t.Board.ViewPolicy & accessPolicy) != BoardAccessPolicy.None)
            .ProjectTo<Topic>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }
}