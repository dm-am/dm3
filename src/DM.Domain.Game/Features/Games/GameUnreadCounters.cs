using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;

namespace DM.Domain.Game.Features.Games;

// A guest marks nothing read, so he is shown the totals in the same fields.
internal sealed class GameUnreadCounters
{
    private readonly IGameRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;

    public GameUnreadCounters(
        IGameRepository repository,
        IUnreadCountersRepository unreadCountersRepository)
    {
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
    }

    public async Task FillTotals(Game[] games)
    {
        var ids = games.Select(g => g.Id).ToArray();
        var postCounts = await _repository.GetTotalPostCounts(ids);
        var commentCounts = await _repository.GetTotalCommentCounts(ids);
        foreach (var game in games)
        {
            game.UnreadPostsCount = postCounts.TryGetValue(game.Id, out var pc) ? pc : 0;
            game.UnreadCommentsCount = commentCounts.TryGetValue(game.Id, out var cc) ? cc : 0;
        }
    }

    public async Task FillUnread(Game[] games, Guid currentUserId)
    {
        await _unreadCountersRepository.FillEntityCounters(games, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);
        await _unreadCountersRepository.FillEntityCounters(games, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);

        var gameIds = games.Select(g => g.Id).ToArray();
        var gameRooms = await _repository.GetAvailableRoomIds(gameIds, currentUserId);
        var allRoomIds = gameRooms.SelectMany(r => r.Value).ToArray();

        if (allRoomIds.Length > 0)
        {
            var unreadPostCounters = await _unreadCountersRepository.SelectByEntitiesAsync(
                currentUserId, UnreadEntryType.Message, allRoomIds);
            foreach (var game in games)
            {
                if (!gameRooms.TryGetValue(game.Id, out var roomIds))
                {
                    continue;
                }

                game.UnreadPostsCount = roomIds.Sum(id =>
                    unreadPostCounters.TryGetValue(id, out var count) ? count : 0);
            }
        }
    }

    public async Task FillCounters(Game game, Guid currentUserId)
    {
        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCommentsCount);
        await _unreadCountersRepository.FillEntityCounters(new[] { game }, currentUserId,
            g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);
    }
}
