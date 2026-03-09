using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Messaging.GeneralBus;

namespace DM.Workers.SearchIndexer.Implementation.Indexing;

/// <summary>
/// Certain indexer for search engine
/// </summary>
internal interface IIndexer
{
    /// <summary>
    /// Tells if this event entity can be indexed
    /// </summary>
    /// <param name="eventType">Event type</param>
    /// <returns>Can be indexed by this indexer</returns>
    bool CanIndex(EventType eventType);

    /// <summary>
    /// Indexes event entity in search engine
    /// </summary>
    /// <param name="message">Event</param>
    /// <returns></returns>
    Task Index(InvokedEvent message);
}