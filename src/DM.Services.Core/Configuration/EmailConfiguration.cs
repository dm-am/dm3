namespace DM.Services.Core.Configuration;

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
}