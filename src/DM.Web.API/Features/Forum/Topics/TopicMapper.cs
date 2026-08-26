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
/// Compile-time mapper from service DTO to API DTO for topics. The board
/// renders through the composed <see cref="BoardMapper"/> - the migration
/// bridge that kept the board maps alive in AutoMapper dies with the profile.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
internal partial class TopicMapper
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
    /// Domain topic to its response DTO. The explicit envelope step replaces
    /// the AutoMapper AfterMap - it must run on every topic that crosses the
    /// API, or the author round-trip silently breaks.
    /// </summary>
    public Topic ToTopic(DomainTopic topic)
    {
        var result = ToTopicCore(topic);

        // Comment surface allows [mod] blocks. Populate the render-context
        // envelope with the topic author so the JSON converter honors the
        // author's AuthorEdit round-trip: getTopicForUpdate (GET
        // topics/{id}) sends X-Dm-Audience: author_edit to fetch the raw
        // BBCode source for the editor. Without the owner id the converter
        // fails closed (Display for everyone) - safe against strangers, but
        // the author-moderator's editor then receives rendered HTML instead
        // of source, and saving it silently unwraps their [mod] markup.
        if (result.Description is not null)
        {
            result.Description.Context = new RenderContextEnvelope
            {
                Surface = result.Description.Surface,
                PostAuthorUserId = topic.Author?.UserId
            };
        }

        return result;
    }

    [MapProperty(nameof(DomainTopic.TotalCommentsCount), nameof(Topic.CommentsCount))]
    [MapProperty(nameof(DomainTopic.Text), nameof(Topic.Description))]
    [MapProperty(nameof(DomainTopic.LastComment), nameof(Topic.LastComment), Use = nameof(ToLastTopicComment))]
    // Filled by TopicApiService.EnrichPeriodDigests (one batch marker lookup
    // per response), not by the per-entity mapping.
    [MapperIgnoreTarget(nameof(Topic.PeriodDigest))]
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

    // A topic with no comments carries a null even though the member is
    // declared `= null!`; the guard keeps the AutoMapper null-through
    // behavior instead of materialising an empty last comment.
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
