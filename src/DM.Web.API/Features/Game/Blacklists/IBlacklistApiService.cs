using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Game.Blacklists;

/// <summary>
/// API service for game blacklist
/// </summary>
public interface IBlacklistApiService
{
    /// <summary>
    /// Get list of blacklisted users for the game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns>List envelope containing blacklisted users</returns>
    Task<ListEnvelope<User>> Get(Guid gameId);

    /// <summary>
    /// Add user to the game blacklist
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="user">User to blacklist</param>
    /// <returns>Envelope containing the blacklisted user</returns>
    Task<Envelope<User>> Create(Guid gameId, User user);

    /// <summary>
    /// Remove user from the blacklist
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="login">User login</param>
    Task Delete(Guid gameId, string login);
}
