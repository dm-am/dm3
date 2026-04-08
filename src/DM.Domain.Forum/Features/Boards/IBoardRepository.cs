using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Forum.Features.Boards;

/// <summary>
/// Boards storage
/// </summary>
public interface IBoardRepository
{
    /// <summary>
    /// Get list of available boards by access policy
    /// </summary>
    /// <param name="accessPolicy">Board access policy</param>
    /// <returns>List of boards matching the access policy</returns>
    Task<IEnumerable<Board>> SelectBoards(BoardAccessPolicy? accessPolicy);
}
