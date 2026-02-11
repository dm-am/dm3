using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Users;

/// <summary>
/// API service for login history
/// </summary>
public interface ILoginHistoryApiService
{
    /// <summary>
    /// Get login history for a user by login
    /// </summary>
    /// <param name="login">User login</param>
    /// <returns>List of login history entries</returns>
    Task<ListEnvelope<LoginHistoryDto>> GetByLogin(string login);
}
