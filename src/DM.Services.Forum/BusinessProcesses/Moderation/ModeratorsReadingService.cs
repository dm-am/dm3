using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Caching;
using DM.Services.Core.Dto;
using DM.Services.Forum.BusinessProcesses.Boards;

namespace DM.Services.Forum.BusinessProcesses.Moderation;

/// <inheritdoc />
internal class ModeratorsReadingService : IModeratorsReadingService
{
    private readonly IBoardReadingService _boardReadingService;
    private readonly IModeratorRepository _moderatorRepository;
    private readonly ICache _cache;

    /// <inheritdoc />
    public ModeratorsReadingService(
        IBoardReadingService boardReadingService,
        IModeratorRepository moderatorRepository,
        ICache cache)
    {
        _boardReadingService = boardReadingService;
        _moderatorRepository = moderatorRepository;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetModerators(string boardTitle)
    {
        var board = await _boardReadingService.GetBoard(boardTitle);
        return await _cache.GetOrCreate(
            $"board_moderators_{board.Id}",
            () => _moderatorRepository.Get(board.Id),
            CachePolicy.LongLived);
    }
}