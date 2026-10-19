namespace DM.Domain.Messaging.Configuration;

public class MessagingConfiguration
{
    /// <summary>
    /// How long the author may edit or delete his own message, in minutes; a moderator has no limit
    /// </summary>
    public int EditTimeoutMinutes { get; set; } = 15;
}
