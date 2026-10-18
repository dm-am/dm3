using System.Linq;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A letter does not open by telling a person their own name.
/// </summary>
/// <remarks>
/// The channels print the payload field by field, and every payload that carries
/// a bare Username carries the name of the reader the letter is addressed to: the
/// ban, the warning, the correction of its points and the four security events.
/// The field is there because the link is built out of it, the same reason the
/// identifiers are carried, so it belongs with them on the hidden list rather
/// than at the top of the letter.
/// </remarks>
public class NotificationMetadataTextShould
{
    [Fact]
    public void LeaveTheAddresseesOwnNameOutOfWhatTheChannelsPrint()
    {
        var written = NotificationWording.ReadMetadata(new
        {
            Username = "SolohinLex",
            ModeratorUsername = "TestSeniorMod",
            Points = 0
        });

        written.Select(pair => pair.Value).Should().NotContain("SolohinLex",
            "the letter goes to that very reader");
        written.Select(pair => pair.Key).Should().Contain("Модератор",
            "the moderator is somebody else, and the letter names them");
    }
}
