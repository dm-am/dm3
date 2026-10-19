using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Forum.Features.Boards;

public interface IBoardRepository
{
    /// <summary>
    /// Get list of boards
    /// </summary>
    Task<IEnumerable<Board>> SelectBoards();
}
