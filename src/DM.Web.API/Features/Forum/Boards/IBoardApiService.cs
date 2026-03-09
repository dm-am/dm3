using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Forum.Boards;

/// <summary>
/// API service for board resources
/// </summary>
public interface IBoardApiService
{
    /// <summary>
    /// Get list of available boards
    /// </summary>
    /// <returns>Envelope with boards list</returns>
    Task<ListEnvelope<Board>> GetBoards();

    /// <summary>
    /// Get board by id
    /// </summary>
    /// <param name="id">Board id</param>
    /// <returns>Envelope with board</returns>
    Task<Envelope<Board>> GetBoard(string id);
}
