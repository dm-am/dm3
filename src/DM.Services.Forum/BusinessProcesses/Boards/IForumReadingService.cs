using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Services.Forum.BusinessProcesses.Boards;

/// <summary>
/// Service for reading boards
/// </summary>
public interface IBoardReadingService
{
    /// <summary>
    /// Get list of available boards
    /// </summary>
    /// <returns></returns>
    Task<IEnumerable<Dto.Output.Board>> GetBoardsList();

    /// <summary>
    /// Get available board by title with counters
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <returns></returns>
    Task<Dto.Output.Board> GetSingleBoard(string boardTitle);

    /// <summary>
    /// Get available board by title with no counters
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <param name="onlyAvailable">Only search in boards that are available for display for current user</param>
    /// <returns></returns>
    Task<Dto.Output.Board> GetBoard(string boardTitle, bool onlyAvailable = true);
}