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
/// Indexer for new forum commentaries
/// </summary>
internal class NewForumCommentIndexer : BaseIndexer
{
    private readonly DmDbContext _dbContext;
    private readonly IBbParserProvider _parserProvider;
    private readonly IIndexingRepository _repository;

    /// <inheritdoc />
    public NewForumCommentIndexer(
        DmDbContext dbContext,
        IBbParserProvider parserProvider,
        IIndexingRepository repository)
    {
        _dbContext = dbContext;
        _parserProvider = parserProvider;
        _repository = repository;
    }

    /// <inheritdoc />
    protected override EventType EventType => EventType.NewForumComment;

    /// <inheritdoc />
    public override async Task Index(InvokedEvent message)
    {
        var comment = await _dbContext.Comments
            .Where(c => c.CommentId == message.EntityId)
            .Select(c => new {c.Topic.Forum.ViewPolicy, c.Topic.ForumTopicId, c.Text})
            .FirstAsync();
        await _repository.Index(new SearchEntity
        {
            Id = message.EntityId,
            ParentEntityId = comment.ForumTopicId,
            EntityType = SearchEntityType.ForumComment,
            Text = _parserProvider.CurrentCommon.Parse(comment.Text).ToHtml(),
            AuthorizedRoles = comment.ViewPolicy.GetAuthorizedRoles()
        });
    }
}