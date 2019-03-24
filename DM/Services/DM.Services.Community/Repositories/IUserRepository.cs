using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Community.Dto;
using DM.Services.Core.Dto;

namespace DM.Services.Community.Repositories
{
    /// <summary>
    /// Community users storage
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Count community users by filter
        /// </summary>
        /// <param name="withInactive">Count inactive users too</param>
        /// <returns>Number of the users</returns>
        Task<int> CountUsers(bool withInactive);

        /// <summary>
        /// Get users list on paging data
        /// </summary>
        /// <param name="paging">Paging data</param>
        /// <param name="withInactive">Search among inactive users</param>
        /// <returns>List of users found</returns>
        Task<IEnumerable<GeneralUser>> GetUsers(PagingData paging, bool withInactive);

        /// <summary>
        /// Get user by login
        /// </summary>
        /// <param name="login">User login</param>
        /// <returns>User found. Null if none found</returns>
        Task<GeneralUser> GetUser(string login);

        /// <summary>
        /// Get user profile by login
        /// </summary>
        /// <param name="login">User login</param>
        /// <returns>User profile found. Null if none found</returns>
        Task<UserProfile> GetProfile(string login);
    }
}