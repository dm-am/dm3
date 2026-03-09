using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Boards;

/// <summary>
/// Service for board operations
/// </summary>
public interface IBoardService
{
    /// <summary>
    /// Get list of available boards
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<Board>> GetBoardsList();

    /// <summary>
    /// Get available board by title with counters
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <returns></returns>
    Task<Board> GetSingleBoard(string boardTitle);

    /// <summary>
    /// Get available board by title with no counters
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <param name="onlyAvailable">Only search in boards that are available for display for current user</param>
    /// <returns></returns>
    Task<Board> GetBoard(string boardTitle, bool onlyAvailable = true);

    /// <summary>
    /// Get list of board moderators by board title
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <returns></returns>
    Task<IEnumerable<GeneralUser>> GetModerators(string boardTitle);
}
