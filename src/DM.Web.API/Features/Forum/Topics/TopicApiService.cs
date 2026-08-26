using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Topics;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Forum.Topics;

/// <inheritdoc />
internal class TopicApiService : ITopicApiService
{
    private readonly ITopicService _topicService;
    private readonly TopicMapper _mapper;
    private readonly IQuoteSourceService _quoteSourceService;

    /// <inheritdoc />
    public TopicApiService(
        ITopicService topicService,
        TopicMapper mapper,
        IQuoteSourceService quoteSourceService)
    {
        _topicService = topicService;
        _mapper = mapper;
        _quoteSourceService = quoteSourceService;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Topic>> Get(string boardId, TopicsQuery query)
    {
        var domainQuery = _mapper.ToTopicsQuery(query);
        var (topics, paging) = await _topicService.GetListAsync(boardId, domainQuery);
        var mapped = topics.Select(_mapper.ToTopic).ToList();
        await EnrichPeriodDigests(mapped);
        return new ListEnvelope<Topic>(mapped, paging != null ? new PagingInfo(paging) : null);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Topic>> GetAcrossBoards(TopicsQuery query)
    {
        var domainQuery = _mapper.ToTopicsQuery(query);
        var (topics, paging) = await _topicService.GetListAcrossBoardsAsync(domainQuery);
        var mapped = topics.Select(_mapper.ToTopic).ToList();
        await EnrichPeriodDigests(mapped);
        return new ListEnvelope<Topic>(mapped, paging != null ? new PagingInfo(paging) : null);
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic?>> GetUserBestTopic(string username)
    {
        var topic = await _topicService.GetBestUserTopicAsync(username);
        if (topic == null) return new Envelope<Topic?>(null);
        var mapped = _mapper.ToTopic(topic);
        await EnrichPeriodDigests([mapped]);
        return new Envelope<Topic?>(mapped);
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic>> Get(Guid topicId)
    {
        var topic = await _topicService.GetAsync(topicId);
        var mapped = _mapper.ToTopic(topic);
        await EnrichPeriodDigests([mapped]);
        return new Envelope<Topic>(mapped);
    }

    /// <inheritdoc />
    public async Task<Envelope<QuoteSource>> GetQuote(Guid topicId)
    {
        // Read through the same service the ordinary read goes through: a topic
        // in a board this reader cannot open is refused by that read, and there
        // is no second permission rule here to keep in step with the first.
        var topic = await _topicService.GetAsync(topicId);
        var mapped = _mapper.ToTopic(topic);
        return _quoteSourceService.Build(mapped.Description, topic.Author?.Username);
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic>> GetByBoardAndNumber(string boardAlias, int topicNumber)
    {
        var topic = await _topicService.GetByBoardAndNumberAsync(boardAlias, topicNumber);
        var mapped = _mapper.ToTopic(topic);
        await EnrichPeriodDigests([mapped]);
        return new Envelope<Topic>(mapped);
    }

    /// <summary>
    /// Marks period-digest topics ("Итоги …") with the period they summarize
    /// (one indexed lookup for the whole batch). The client renders the
    /// period's leaderboards inside such topics from the statistics API.
    /// </summary>
    private async Task EnrichPeriodDigests(IReadOnlyCollection<Topic> topics)
    {
        if (topics.Count == 0) return;
        var ids = topics.Select(t => t.Id).ToList();
        var byTopic = await _topicService.GetPeriodDigestsAsync(ids);
        if (byTopic.Count == 0) return;
        foreach (var topic in topics)
        {
            if (byTopic.TryGetValue(topic.Id, out var marker))
            {
                topic.PeriodDigest = new PeriodDigestRef
                {
                    Year = marker.Year,
                    Month = marker.Month,
                };
            }
        }
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic>> Create(string boardId, CreateTopicRequest request)
    {
        var createTopic = _mapper.ToCreateTopic(request);
        createTopic.BoardTitle = boardId;
        var createdTopic = await _topicService.CreateAsync(createTopic);
        return new Envelope<Topic>(_mapper.ToTopic(createdTopic));
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic>> Update(Guid topicId, UpdateTopicRequest request)
    {
        var updateTopic = _mapper.ToUpdateTopic(request);
        updateTopic.TopicId = topicId;
        var updatedTopic = await _topicService.UpdateAsync(updateTopic);
        var mapped = _mapper.ToTopic(updatedTopic);
        // The client stores commit PATCH responses wholesale — without the
        // marker a digest topic would lose its boards until a reload.
        await EnrichPeriodDigests([mapped]);
        return new Envelope<Topic>(mapped);
    }

    /// <inheritdoc />
    public Task Delete(Guid topicId) => _topicService.DeleteAsync(topicId);

    /// <inheritdoc />
    public Task ReorderPinned(string boardId, ReorderPinnedRequest request) =>
        _topicService.ReorderPinnedAsync(boardId, request.TopicIds);
}
