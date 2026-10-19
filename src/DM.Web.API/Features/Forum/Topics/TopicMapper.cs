using DM.Domain.Forum.Authorization;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Forum.Boards;
using DM.Web.API.Shared.BbRendering;
using Riok.Mapperly.Abstractions;
using DomainTopic = DM.Domain.Forum.Features.Topics.Topic;
using DomainLastComment = DM.Domain.Forum.Features.Topics.LastComment;
using DomainCreateTopic = DM.Domain.Forum.Features.Topics.CreateTopic;
using DomainUpdateTopic = DM.Domain.Forum.Features.Topics.UpdateTopic;
using DomainTopicsQuery = DM.Domain.Forum.Features.Topics.TopicsQuery;

namespace DM.Web.API.Features.Forum.Topics;

/// <summary>
/// Compile-time mapper from service DTO to API DTO for topics; the board renders
/// through the composed <see cref="BoardMapper"/>.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
public partial class TopicMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    [UseMapper]
    private readonly BoardMapper _boardMapper;

    public TopicMapper(UserMapper userMapper, BoardMapper boardMapper)
    {
        _userMapper = userMapper;
        _boardMapper = boardMapper;
    }

    /// <summary>
    /// Domain topic to its response DTO, with the author bound to the text: an
    /// editor that got rendered HTML instead of the source would unwrap [mod]
    /// markup on save.
    /// </summary>
    public Topic ToTopic(DomainTopic topic)
    {
        var result = ToTopicCore(topic);
        result.Description?.BindAuthor(topic.Author?.UserId,
            intentions => intentions.IsAllowed(TopicIntention.Edit, topic));
        return result;
    }

    [MapProperty(nameof(DomainTopic.TotalCommentsCount), nameof(Topic.CommentsCount))]
    [MapProperty(nameof(DomainTopic.Text), nameof(Topic.Description))]
    [MapProperty(nameof(DomainTopic.LastComment), nameof(Topic.LastComment), Use = nameof(ToLastTopicComment))]
    // Both filled by TopicApiService: the digest marker per batch, the viewer per topic.
    [MapperIgnoreTarget(nameof(Topic.PeriodDigest))]
    [MapperIgnoreTarget(nameof(Topic.Viewer))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Topic ToTopicCore(DomainTopic topic);

    /// <summary>
    /// Create request to the write model. BoardTitle comes from the route,
    /// the service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainCreateTopic.BoardTitle))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainCreateTopic ToCreateTopic(CreateTopicRequest request);

    /// <summary>
    /// Update request to the write model. TopicId comes from the route, the
    /// service sets it. The domain resolves a board by alias, title or id and
    /// compares the incoming value with the current board title to decide
    /// whether this is a move - so the board field maps as the free-form
    /// BoardTitle, never as an id.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainUpdateTopic.TopicId))]
    [MapProperty(nameof(UpdateTopicRequest.Description), nameof(DomainUpdateTopic.Text))]
    [MapProperty(nameof(UpdateTopicRequest.Board), nameof(DomainUpdateTopic.BoardTitle))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainUpdateTopic ToUpdateTopic(UpdateTopicRequest request);

    /// <summary>
    /// API filter query to the domain one
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainTopicsQuery ToTopicsQuery(TopicsQuery query);

    // A topic with no comments carries a null from a `= null!` member.
    private LastTopicComment ToLastTopicComment(DomainLastComment? comment) =>
        comment == null
            ? null!
            : new LastTopicComment
            {
                Id = comment.Id,
                CreatedUtc = comment.CreatedUtc,
                Author = _userMapper.ToUser(comment.Author)
            };
}
