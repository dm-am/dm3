using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Storage for token verification
/// </summary>
public interface ITokenVerificationRepository
{
    /// <summary>
    /// Get user that token was generated for
    /// </summary>
    Task<GeneralUser?> GetTokenOwner(Guid tokenId);
}
