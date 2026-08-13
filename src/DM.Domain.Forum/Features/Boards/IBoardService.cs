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
    /// <returns>List of boards</returns>
    Task<IEnumerable<Board>> GetBoardsList();

    /// <summary>
    /// Get list of available boards without their unread counters
    /// </summary>
    /// <remarks>
    /// The counters of the list above cost two aggregate reads across the document
    /// store and three more queries behind them, and a caller that only needs to
    /// know which boards exist throws every one of those away. Named apart rather
    /// than parameterised so that the choice is made where it is paid for.
    /// </remarks>
    /// <returns>List of boards</returns>
    Task<IEnumerable<Board>> GetAvailableBoards();

    /// <summary>
    /// Get available board by title with counters
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <returns>Board with counters</returns>
    Task<Board> GetSingleBoard(string boardTitle);

    /// <summary>
    /// Get available board by title with no counters
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <param name="onlyAvailable">Only search in boards that are available for display for current user</param>
    /// <returns>Board without counters</returns>
    Task<Board> GetBoard(string boardTitle, bool onlyAvailable = true);

    /// <summary>
    /// Get list of board moderators by board title
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <returns>List of moderators</returns>
    Task<IEnumerable<GeneralUser>> GetModerators(string boardTitle);

    /// <summary>
    /// Add a user as board moderator
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <param name="username">Username to add</param>
    /// <returns>Added user</returns>
    Task<GeneralUser> AddModerator(string boardTitle, string username);

    /// <summary>
    /// Remove a user from board moderators
    /// </summary>
    /// <param name="boardTitle">Board title</param>
    /// <param name="username">Username to remove</param>
    /// <returns>Task</returns>
    Task RemoveModerator(string boardTitle, string username);

    /// <summary>
    /// Get board by alias only (strict lookup)
    /// </summary>
    /// <param name="alias">Board alias</param>
    /// <returns>Board</returns>
    Task<Board> GetBoardByAlias(string alias);
}
