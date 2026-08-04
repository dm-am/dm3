using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Service for token verification
/// </summary>
public interface ITokenVerificationService
{
    /// <summary>
    /// Verify if the token is available
    /// </summary>
    /// <param name="token">Token</param>
    /// <returns>User associated with the token</returns>
    Task<GeneralUser> Verify(Guid token);
}
