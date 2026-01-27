using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Forum.BusinessProcesses.Boards;

/// <summary>
/// Boards storage
/// </summary>
internal interface IBoardRepository
{
    /// <summary>
    /// Get list of available boards by access policy
    /// </summary>
    /// <param name="accessPolicy">Board access policy</param>
    /// <returns></returns>
    Task<IEnumerable<Dto.Output.Board>> SelectBoards(BoardAccessPolicy? accessPolicy);
}