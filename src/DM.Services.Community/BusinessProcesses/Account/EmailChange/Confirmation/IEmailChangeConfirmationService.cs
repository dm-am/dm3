using System;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Account.EmailChange.Confirmation;

/// <summary>
/// Service for confirming email change via token
/// </summary>
public interface IEmailChangeConfirmationService
{
    /// <summary>
    /// Confirm email change by token
    /// </summary>
    /// <param name="tokenId">Email change confirmation token</param>
    Task Confirm(Guid tokenId);
}
