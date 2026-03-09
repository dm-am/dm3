using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Persistence;
using DM.Domain.Core.Search;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Domain.Forum.Features.Boards;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.SearchIndexer.Implementation.Indexing.Indexers;

/// <summary>
/// Indexer for changed topic comments
/// </summary>
internal class TopicCommentChangedIndexer : BaseIndexer
{
    private readonly DmDbContext _dbContext;
    private readonly IBbParserProvider _bbParserProvider;
    private readonly IIndexingRepository _indexingRepository;

    /// <inheritdoc />
    public TopicCommentChangedIndexer(
        DmDbContext dbContext,
        IBbParserProvider bbParserProvider,
        IIndexingRepository indexingRepository)
    {
        _dbContext = dbContext;
        _bbParserProvider = bbParserProvider;
        _indexingRepository = indexingRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.ChangedTopicComment;

    /// <inheritdoc />
    public override async Task Index(InvokedEvent message)
    {
        var comment = await _dbContext.Comments
            .Where(c => c.CommentId == message.EntityId)
            .Select(c => new {c.Text, c.Topic!.Board.ViewPolicy, c.Topic!.TopicId})
            .FirstAsync();

        await _indexingRepository.Index(new SearchEntity
        {
            Id = message.EntityId,
            ParentEntityId = comment.TopicId,
            EntityType = SearchEntityType.ForumComment,
            Text = _bbParserProvider.CurrentCommon.Parse(comment.Text).ToHtml(),
            AuthorizedRoles = comment.ViewPolicy.GetAuthorizedRoles()
        });
    }
}
