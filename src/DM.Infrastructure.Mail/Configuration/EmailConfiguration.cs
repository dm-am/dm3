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
    /// Whether the relay is to be authenticated against at all.
    /// </summary>
    /// <remarks>
    /// A blank user name, as in alertmanager.yml: MailKit refuses to authenticate against a relay without AUTH.
    /// </remarks>
    public bool UsesAuthentication => !string.IsNullOrWhiteSpace(Username);

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
    /// Off by default for Mailpit, which offers no TLS; opportunistic STARTTLS can be stripped on the path.
    /// </remarks>
    public bool RequireTls { get; set; }
}
