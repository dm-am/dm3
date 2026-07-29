using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Forum.Features.Topics;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Forum.Boards;
using DomainCreateTopic = DM.Domain.Forum.Features.Topics.CreateTopic;
using DomainUpdateTopic = DM.Domain.Forum.Features.Topics.UpdateTopic;
using DomainTopicsQuery = DM.Domain.Forum.Features.Topics.TopicsQuery;

namespace DM.Web.API.Features.Forum.Topics;

/// <inheritdoc />
internal class TopicApiService : ITopicApiService
{
    private readonly ITopicService _topicService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public TopicApiService(
        ITopicService topicService,
        IMapper mapper)
    {
        _topicService = topicService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Topic>> Get(string boardId, TopicsQuery query)
    {
        var domainQuery = _mapper.Map<DomainTopicsQuery>(query);
        var (topics, paging) = await _topicService.GetListAsync(boardId, domainQuery);
        var mapped = topics.Select(_mapper.Map<Topic>).ToList();
        await EnrichPeriodDigests(mapped);
        return new ListEnvelope<Topic>(mapped, paging != null ? new PagingInfo(paging) : null);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Topic>> GetAcrossBoards(TopicsQuery query)
    {
        var domainQuery = _mapper.Map<DomainTopicsQuery>(query);
        var (topics, paging) = await _topicService.GetListAcrossBoardsAsync(domainQuery);
        var mapped = topics.Select(_mapper.Map<Topic>).ToList();
        await EnrichPeriodDigests(mapped);
        return new ListEnvelope<Topic>(mapped, paging != null ? new PagingInfo(paging) : null);
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic?>> GetUserBestTopic(string username)
    {
        var topic = await _topicService.GetBestUserTopicAsync(username);
        if (topic == null) return new Envelope<Topic?>(null);
        var mapped = _mapper.Map<Topic>(topic);
        await EnrichPeriodDigests([mapped]);
        return new Envelope<Topic?>(mapped);
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic>> Get(Guid topicId)
    {
        var topic = await _topicService.GetAsync(topicId);
        var mapped = _mapper.Map<Topic>(topic);
        await EnrichPeriodDigests([mapped]);
        return new Envelope<Topic>(mapped);
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic>> GetByBoardAndNumber(string boardAlias, int topicNumber)
    {
        var topic = await _topicService.GetByBoardAndNumberAsync(boardAlias, topicNumber);
        var mapped = _mapper.Map<Topic>(topic);
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
        var createTopic = _mapper.Map<DomainCreateTopic>(request);
        createTopic.BoardTitle = boardId;
        var createdTopic = await _topicService.CreateAsync(createTopic);
        return new Envelope<Topic>(_mapper.Map<Topic>(createdTopic));
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic>> Update(Guid topicId, UpdateTopicRequest request)
    {
        var updateTopic = _mapper.Map<DomainUpdateTopic>(request);
        updateTopic.TopicId = topicId;
        var updatedTopic = await _topicService.UpdateAsync(updateTopic);
        var mapped = _mapper.Map<Topic>(updatedTopic);
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
