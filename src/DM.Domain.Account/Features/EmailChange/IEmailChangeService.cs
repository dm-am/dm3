using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Account.Features.EmailChange;

/// <summary>
/// Service for email change operations
/// </summary>
public interface IEmailChangeService
{
    /// <summary>
    /// Request email change (sends confirmation to new email)
    /// </summary>
    /// <param name="emailChange">Email change request</param>
    /// <returns>User info</returns>
    Task<GeneralUser> Change(UserEmailChange emailChange);

    /// <summary>
    /// Confirm email change by token
    /// </summary>
    /// <param name="tokenId">Email change confirmation token</param>
    Task Confirm(Guid tokenId);
}
