namespace DM.Domain.Messaging.Configuration;

/// <summary>
/// Messaging business rules configuration
/// </summary>
public class MessagingConfiguration
{
    /// <summary>
    /// Time limit in minutes for message authors to edit/delete their own messages.
    /// After this time, only moderators can edit/delete.
    /// Default: 15 minutes
    /// </summary>
    public int EditTimeoutMinutes { get; set; } = 15;
}
