using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Messaging.GeneralBus;

namespace DM.Workers.SearchIndexer.Implementation.Indexing.Indexers;

/// <summary>
/// Indexer for deleted topic comments
/// </summary>
internal class TopicCommentDeletedIndexer : BaseIndexer
{
    private readonly IIndexingRepository _indexingRepository;

    /// <inheritdoc />
    public TopicCommentDeletedIndexer(
        IIndexingRepository indexingRepository)
    {
        _indexingRepository = indexingRepository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.DeletedTopicComment;

    /// <inheritdoc />
    public override Task Index(InvokedEvent message)
    {
        return _indexingRepository.Delete(message.EntityId);
    }
}
