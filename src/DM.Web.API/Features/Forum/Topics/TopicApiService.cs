using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Forum.Features.Topics;
using DM.Web.API.Features.Forum.Boards;
using DM.Web.API.Shared.Dto;
using DomainTopic = DM.Domain.Forum.Features.Topics.Topic;

namespace DM.Web.API.Features.Forum.Topics;

/// <inheritdoc />
internal class TopicApiService : ITopicApiService
{
    private readonly ITopicService _topicService;
    private readonly ITopicCommentService _commentService;
    private readonly TopicMapper _mapper;

    /// <inheritdoc />
    public TopicApiService(
        ITopicService topicService,
        ITopicCommentService commentService,
        TopicMapper mapper)
    {
        _topicService = topicService;
        _commentService = commentService;
        _mapper = mapper;
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
        var mapped = TopicForViewer(topic);
        await EnrichPeriodDigests([mapped]);
        return new Envelope<Topic?>(mapped);
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic>> Get(Guid topicId)
    {
        var topic = await _topicService.GetAsync(topicId);
        var mapped = TopicForViewer(topic);
        await EnrichPeriodDigests([mapped]);
        return new Envelope<Topic>(mapped);
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic>> GetByBoardAndNumber(string boardAlias, int topicNumber)
    {
        var topic = await _topicService.GetByBoardAndNumberAsync(boardAlias, topicNumber);
        var mapped = TopicForViewer(topic);
        await EnrichPeriodDigests([mapped]);
        return new Envelope<Topic>(mapped);
    }

    /// <summary>
    /// One topic as the viewer gets it: the topic, and what he may do under it.
    /// The reads of one topic and the writes that return one come through here;
    /// a list of topics offers nothing to write and leaves it out. The edit
    /// matters most: closing the topic is a PATCH, and the page replaces the
    /// topic it holds with the one the PATCH returns. The topic's board carries
    /// what the viewer may do on it as well: the forum page takes it for the
    /// board it holds when the topic is opened first.
    /// </summary>
    private Topic TopicForViewer(DomainTopic topic)
    {
        var mapped = _mapper.ToTopic(topic);
        mapped.Viewer = new TopicViewer { CanComment = _commentService.MayCreate(topic) };
        mapped.Board.Viewer = BoardApiService.ViewerOf(_topicService, topic.Board);
        return mapped;
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
        return new Envelope<Topic>(TopicForViewer(createdTopic));
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic>> Update(Guid topicId, UpdateTopicRequest request)
    {
        var updateTopic = _mapper.ToUpdateTopic(request);
        updateTopic.TopicId = topicId;
        var updatedTopic = await _topicService.UpdateAsync(updateTopic);
        var mapped = TopicForViewer(updatedTopic);
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
