using System;
using System.Threading.Tasks;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Users;

/// <summary>
/// API service for user email change
/// </summary>
public interface IEmailChangeApiService
{
    /// <summary>
    /// Change user email (requires password verification)
    /// </summary>
    Task<Envelope<User>> Change(ChangeEmail changeEmail);

    /// <summary>
    /// Confirm email change by token
    /// </summary>
    Task ConfirmEmailChange(Guid token);
}