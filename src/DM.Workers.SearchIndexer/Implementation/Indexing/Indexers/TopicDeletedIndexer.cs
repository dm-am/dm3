using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Messaging.GeneralBus;

namespace DM.Workers.SearchIndexer.Implementation.Indexing.Indexers;

/// <summary>
/// Indexer for removed topic
/// </summary>
internal class TopicDeletedIndexer : BaseIndexer
{
    private readonly IIndexingRepository _repository;

    /// <inheritdoc />
    public TopicDeletedIndexer(
        IIndexingRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.DeletedTopic;

    /// <inheritdoc />
    public override Task Index(InvokedEvent message)
    {
        return _repository.DeleteByParent(message.EntityId);
    }
}