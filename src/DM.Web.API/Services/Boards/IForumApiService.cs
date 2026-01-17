using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Boards;

namespace DM.Web.API.Services.Boards;

/// <summary>
/// API service for forum/board resources
/// </summary>
public interface IForumApiService
{
    /// <summary>
    /// Get list of available fora (legacy)
    /// </summary>
    /// <returns>Envelope with fora list</returns>
    Task<ListEnvelope<Forum>> Get();

    /// <summary>
    /// Get forum by id (legacy)
    /// </summary>
    /// <param name="id">Forum id</param>
    /// <returns>Envelope with forum</returns>
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