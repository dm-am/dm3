using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Messaging.GeneralBus;

namespace DM.Workers.SearchIndexer.Implementation.Indexing;

/// <inheritdoc />
public abstract class BaseIndexer : IIndexer
{
    /// <summary>
    /// Event type that this indexer can process
    /// </summary>
    protected abstract EventType EventType { get; }

    /// <inheritdoc />
    public bool CanIndex(EventType eventType) => eventType == EventType;

    /// <inheritdoc />
    public abstract Task Index(InvokedEvent message);
}