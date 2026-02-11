using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.Dto.Output;
using Comment = DM.Services.Common.Dto.Comment;

namespace DM.Services.Game.BusinessProcesses.Commentaries.Reading;

/// <inheritdoc />
internal class CommentaryReadingService : ICommentaryReadingService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IGameReadingService _gameReadingService;
    private readonly ICommentaryReadingRepository _commentaryRepository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public CommentaryReadingService(
        IIntentionManager intentionManager,
        IGameReadingService gameReadingService,
        ICommentaryReadingRepository commentaryRepository,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider)
    {
        _intentionManager = intentionManager;
        _gameReadingService = gameReadingService;
        _commentaryRepository = commentaryRepository;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
    }
        
    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> comments, PagingResult paging, Dto.Output.Game game)> Get(
        Guid gameId, PagingQuery query)
    {
        var game = await _gameReadingService.GetGame(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.ReadComments, game);

        var totalCount = await _commentaryRepository.Count(gameId);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _commentaryRepository.Get(gameId, paging);

        return (comments, paging.Result, game);
    }

    /// <inheritdoc />
    public async Task<Comment> Get(Guid commentId)
    {
        return await _commentaryRepository.Get(commentId) ??
               throw new HttpException(HttpStatusCode.Gone, $"Comment {commentId} not found");
    }

    /// <inheritdoc />
    public async Task MarkAsRead(Guid gameId)
    {
        var game = await _gameReadingService.GetGame(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.ReadComments, game);
        await _unreadCountersRepository.Flush(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, gameId);
    }
}