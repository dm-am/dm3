using System.Linq;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Parsing;
using DM.Services.DataAccess;
using DM.Services.DataAccess.SearchEngine;
using DM.Services.MessageQueuing.GeneralBus;
using DM.Services.Search.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Search.Consumer.Implementation.Indexing.Indexers;

/// <summary>
/// Indexer for newly created topics
/// </summary>
internal class NewTopicIndexer : BaseIndexer
{
    private readonly DmDbContext _dbContext;
    private readonly IBbParserProvider _parserProvider;
    private readonly IIndexingRepository _repository;

    /// <inheritdoc />
    public NewTopicIndexer(
        DmDbContext dbContext,
        IBbParserProvider parserProvider,
        IIndexingRepository repository)
    {
        _dbContext = dbContext;
        _parserProvider = parserProvider;
        _repository = repository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewForumTopic;

    /// <inheritdoc />
    public override async Task Index(InvokedEvent message)
    {
        var topic = await _dbContext.Topics
            .Where(t => t.TopicId == message.EntityId)
            .Select(t => new {t.Board.ViewPolicy, t.Title, t.Text})
            .FirstAsync();
        await _repository.Index(new SearchEntity
        {
            Id = message.EntityId,
            ParentEntityId = message.EntityId,
            EntityType = SearchEntityType.Topic,
            Title = topic.Title,
            Text = _parserProvider.CurrentCommon.Parse(topic.Text).ToHtml(),
            AuthorizedRoles = topic.ViewPolicy.GetAuthorizedRoles()
        });
    }
}