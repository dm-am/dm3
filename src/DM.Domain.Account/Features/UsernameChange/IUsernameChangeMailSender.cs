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
    Task SendApprovalAsync(string email, string username, string approvalLink);

    /// <summary>
    /// Send notification about request rejection
    /// </summary>
    Task SendRejectionAsync(string email, string username, string? reason);
}
