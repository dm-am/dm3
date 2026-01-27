using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Boards;

namespace DM.Web.API.Services.Boards;

/// <summary>
/// API service for board resources
/// </summary>
public interface IBoardApiService
{
    /// <summary>
    /// Get list of available boards (legacy Forum response)
    /// </summary>
    /// <returns>Envelope with boards list</returns>
    Task<ListEnvelope<Forum>> Get();

    /// <summary>
    /// Get board by id (legacy Forum response)
    /// </summary>
    /// <param name="id">Board id</param>
    /// <returns>Envelope with board</returns>
    Task<Envelope<Forum>> Get(string id);

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