using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Infrastructure.Persistence;
using DM.Domain.Core.Search;
using DM.Infrastructure.Messaging.GeneralBus;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.SearchIndexer.Implementation.Indexing.Indexers;

/// <summary>
/// Indexer for new games
/// </summary>
internal class NewGameIndexer : BaseIndexer
{
    private readonly DmDbContext _dbContext;
    private readonly IBbParserProvider _bbParserProvider;
    private readonly IIndexingRepository _repository;

    /// <inheritdoc />
    public NewGameIndexer(
        DmDbContext dbContext,
        IBbParserProvider bbParserProvider,
        IIndexingRepository repository)
    {
        _dbContext = dbContext;
        _bbParserProvider = bbParserProvider;
        _repository = repository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewGame;

    /// <inheritdoc />
    public override async Task Index(InvokedEvent message)
    {
        var game = await _dbContext.Games
            .Where(g => g.GameId == message.EntityId)
            .Select(g => new {g.GameId, g.Title, g.Info, g.Status, g.MasterId})
            .FirstAsync();
        await _repository.Index(new SearchEntity
        {
            Id = game.GameId,
            EntityType = SearchEntityType.Game,
            Title = game.Title,
            Text = _bbParserProvider.CurrentInfo.Parse(game.Info).ToHtml(),
            AuthorizedUsers = game.Status == ModuleStatus.Draft
                ? [game.MasterId]
                : []
        });
    }
}