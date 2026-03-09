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
/// Indexer for new topic comments
/// </summary>
internal class NewTopicCommentIndexer : BaseIndexer
{
    private readonly DmDbContext _dbContext;
    private readonly IBbParserProvider _parserProvider;
    private readonly IIndexingRepository _repository;

    /// <inheritdoc />
    public NewTopicCommentIndexer(
        DmDbContext dbContext,
        IBbParserProvider parserProvider,
        IIndexingRepository repository)
    {
        _dbContext = dbContext;
        _parserProvider = parserProvider;
        _repository = repository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewTopicComment;

    /// <inheritdoc />
    public override async Task Index(InvokedEvent message)
    {
        var comment = await _dbContext.Comments
            .Where(c => c.CommentId == message.EntityId)
            .Select(c => new {c.Topic!.Board.ViewPolicy, c.Topic!.TopicId, c.Text})
            .FirstAsync();
        await _repository.Index(new SearchEntity
        {
            Id = message.EntityId,
            ParentEntityId = comment.TopicId,
            EntityType = SearchEntityType.ForumComment,
            Text = _parserProvider.CurrentCommon.Parse(comment.Text).ToHtml(),
            AuthorizedRoles = comment.ViewPolicy.GetAuthorizedRoles()
        });
    }
}
