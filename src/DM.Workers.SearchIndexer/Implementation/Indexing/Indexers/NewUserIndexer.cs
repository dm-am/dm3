using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Domain.Core.Search;
using DM.Infrastructure.Messaging.GeneralBus;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.SearchIndexer.Implementation.Indexing.Indexers;

/// <inheritdoc />
internal class NewUserIndexer : BaseIndexer
{
    private readonly DmDbContext _dbContext;
    private readonly IIndexingRepository _repository;

    /// <inheritdoc />
    public NewUserIndexer(
        DmDbContext dbContext,
        IIndexingRepository repository)
    {
        _dbContext = dbContext;
        _repository = repository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.ActivatedUser;

    /// <inheritdoc />
    public override async Task Index(InvokedEvent message)
    {
        var userInfo = await _dbContext.Users
            .Where(u => u.UserId == message.EntityId)
            .Select(u => new {u.Username, u.Name})
            .FirstAsync();

        await _repository.Index(new SearchEntity
        {
            Id = message.EntityId,
            Title = userInfo.Username,
            Text = userInfo.Name ?? string.Empty,
            EntityType = SearchEntityType.User
        });
    }
}