using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Forum.Features.Topics;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Forum.Boards;
using DomainCreateTopic = DM.Domain.Forum.Features.Topics.CreateTopic;
using DomainUpdateTopic = DM.Domain.Forum.Features.Topics.UpdateTopic;
using DomainPagingQuery = DM.Domain.Core.Dto.PagingQuery;

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
        var (topics, paging) = query.IsAttached
            ? (await _topicService.GetAttachedAsync(boardId), null)
            : await _topicService.GetListAsync(boardId, _mapper.Map<DomainPagingQuery>(query));
        return new ListEnvelope<Topic>(topics.Select(_mapper.Map<Topic>), paging != null ? new PagingInfo(paging) : null);
    }

    /// <inheritdoc />
    public async Task<Envelope<Topic>> Get(Guid topicId)
    {
        var topic = await _topicService.GetAsync(topicId);
        return new Envelope<Topic>(_mapper.Map<Topic>(topic));
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
    public async Task<Envelope<Topic>> Update(Guid topicId, Topic topic)
    {
        var updateTopic = _mapper.Map<DomainUpdateTopic>(topic);
        updateTopic.TopicId = topicId;
        var updatedTopic = await _topicService.UpdateAsync(updateTopic);
        return new Envelope<Topic>(_mapper.Map<Topic>(updatedTopic));
    }

    /// <inheritdoc />
    public Task Delete(Guid topicId) => _topicService.DeleteAsync(topicId);
}
