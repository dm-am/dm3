namespace DM.Domain.Core.Mail;

/// <summary>
/// Provides embedded assets for email templates.
/// </summary>
public interface IEmailAssetsProvider
{
    /// <summary>
    /// Get the logo as a linked resource for CID embedding
    /// </summary>
    LinkedResource GetLogo();
}
