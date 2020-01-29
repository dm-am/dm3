using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.PasswordReset
{
    /// <summary>
    /// Storage for password resetting
    /// </summary>
    public interface IPasswordResetRepository
    {
        /// <summary>
        /// Create password restoration token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        Task CreateToken(Token token);
    }
}