using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Forum.Features.Boards;

public interface IBoardService
{
    /// <summary>
    /// Get list of boards
    /// </summary>
    Task<IEnumerable<Board>> GetBoardsList();

    /// <summary>
    /// Get list of boards without their unread counters
    /// </summary>
    /// <remarks>
    /// The counters of the list above cost two aggregate reads across the document
    /// store and three more queries behind them, and a caller that only needs to
    /// know which boards exist throws every one of those away. Named apart rather
    /// than parameterised so that the choice is made where it is paid for.
    /// </remarks>
    Task<IEnumerable<Board>> GetBoardsWithoutCounters();

    /// <summary>
    /// Get board by alias, title or id with counters
    /// </summary>
    Task<Board> GetBoardWithCounters(string aliasOrTitle);

    /// <summary>
    /// Get board by alias, title or id with no counters
    /// </summary>
    Task<Board> GetBoard(string aliasOrTitle);

    Task<IEnumerable<GeneralUser>> GetModerators(string aliasOrTitle);

    /// <returns>Added user</returns>
    Task<GeneralUser> AddModerator(string aliasOrTitle, string username);

    Task RemoveModerator(string aliasOrTitle, string username);

    /// <summary>
    /// Get board by alias only (strict lookup)
    /// </summary>
    Task<Board> GetBoardByAlias(string alias);
}
