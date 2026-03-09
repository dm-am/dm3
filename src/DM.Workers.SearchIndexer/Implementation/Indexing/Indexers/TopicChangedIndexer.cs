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
/// Indexer for modified topic
/// </summary>
internal class TopicChangedIndexer : BaseIndexer
{
    private readonly DmDbContext _dbContext;
    private readonly IBbParserProvider _parserProvider;
    private readonly IIndexingRepository _repository;

    /// <inheritdoc />
    public TopicChangedIndexer(
        DmDbContext dbContext,
        IBbParserProvider parserProvider,
        IIndexingRepository repository)
    {
        _dbContext = dbContext;
        _parserProvider = parserProvider;
        _repository = repository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.ChangedTopic;

    /// <inheritdoc />
    public override async Task Index(InvokedEvent message)
    {
        var topic = await _dbContext.Topics
            .Where(t => t.TopicId == message.EntityId)
            .Select(t => new {t.Board.ViewPolicy, t.Title, t.Text})
            .FirstAsync();
        var authorizedRoles = topic.ViewPolicy.GetAuthorizedRoles().ToArray();

        await _repository.UpdateByParent(message.EntityId, authorizedRoles);
        await _repository.Index(new SearchEntity
        {
            Id = message.EntityId,
            ParentEntityId = message.EntityId,
            EntityType = SearchEntityType.Topic,
            Title = topic.Title,
            Text = _parserProvider.CurrentCommon.Parse(topic.Text).ToHtml(),
            AuthorizedRoles = authorizedRoles
        });
    }
}