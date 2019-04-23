using System;
using System.Threading.Tasks;

namespace DM.Services.Mail.Consumer.Processes.Registration
{
    /// <summary>
    /// Repository for registration email sending
    /// </summary>
    public interface IRegistrationRepository
    {
        /// <summary>
        /// Get registration information
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <returns></returns>
        Task<RegistrationMailViewModel> Get(Guid userId);
    }
}