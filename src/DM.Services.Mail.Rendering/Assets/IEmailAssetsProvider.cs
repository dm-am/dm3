using DM.Services.Mail.Sender;

namespace DM.Services.Mail.Rendering.Assets;

/// <summary>
/// Provides embedded assets for email templates
/// </summary>
public interface IEmailAssetsProvider
{
    /// <summary>
    /// Get the logo as a linked resource for CID embedding
    /// </summary>
    LinkedResource GetLogo();
}
