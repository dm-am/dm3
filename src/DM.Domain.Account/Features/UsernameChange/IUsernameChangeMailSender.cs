using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// Sends mail about username change request resolution
/// </summary>
public interface IUsernameChangeMailSender
{
    /// <summary>
    /// Send notification about request approval
    /// </summary>
    /// <remarks>
    /// Takes the token rather than a finished link: the address of the site is
    /// deployment configuration, and a caller that builds the link itself has
    /// to know it. Every other sender in this module builds its own link the
    /// same way, from SiteAddressConfiguration.
    /// </remarks>
    Task SendApprovalAsync(string email, string username, Guid approvalToken);

    /// <summary>
    /// Send notification about request rejection
    /// </summary>
    Task SendRejectionAsync(string email, string username, string? reason);
}
