namespace DM.Web.API.Features.Account;

/// <summary>
/// A letter with a link went out
/// </summary>
public class MailedLink
{
    /// <summary>
    /// How long the link lives, in hours: the receipt on the screen says it as the letter does
    /// </summary>
    /// <example>48</example>
    public int LinkLifetimeHours { get; set; }
}
