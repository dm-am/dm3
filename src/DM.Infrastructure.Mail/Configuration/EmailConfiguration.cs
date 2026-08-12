namespace DM.Infrastructure.Mail.Configuration;

/// <summary>
/// SMTP configuration
/// </summary>
public class EmailConfiguration
{
    /// <summary>
    /// SMTP server host
    /// </summary>
    public string ServerHost { get; set; } = null!;

    /// <summary>
    /// SMTP server port
    /// </summary>
    public int ServerPort { get; set; }

    /// <summary>
    /// SMTP user name
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// SMTP user password
    /// </summary>
    public string Password { get; set; } = null!;

    /// <summary>
    /// Send emails from address
    /// </summary>
    public string FromAddress { get; set; } = null!;

    /// <summary>
    /// Display name for emails sender
    /// </summary>
    public string FromDisplayName { get; set; } = null!;

    /// <summary>
    /// Send emails with reply to address
    /// </summary>
    public string ReplyToAddress { get; set; } = null!;

    /// <summary>
    /// Refuse to send at all unless the session is encrypted
    /// </summary>
    /// <remarks>
    /// Off by default because the local and preview stacks send through MailHog,
    /// which offers no TLS and would refuse every letter under a stricter
    /// setting. Opportunistic upgrade is also a downgrade anyone on the path can
    /// force by stripping STARTTLS out of the greeting, and what travels in these
    /// letters is activation and password reset links - so a deployment with a
    /// real relay behind it turns this on and the session fails instead of
    /// quietly continuing in the clear.
    /// </remarks>
    public bool RequireTls { get; set; }
}
