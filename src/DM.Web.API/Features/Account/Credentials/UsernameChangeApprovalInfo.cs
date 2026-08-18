namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// State of a username change approval token
/// </summary>
public class UsernameChangeApprovalInfo
{
    /// <summary>
    /// Token status: "ready" (name can be chosen), "expired" (the window closed),
    /// "used" (the name was already changed through this link)
    /// </summary>
    /// <example>ready</example>
    public required string Status { get; set; }

    /// <summary>
    /// Name being changed. Present for "ready" only.
    /// </summary>
    /// <example>reader</example>
    public string? CurrentUsername { get; set; }
}
