using System.Linq;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A letter does not open by telling a person their own name.
/// </summary>
/// <remarks>A bare Username names the reader the letter is addressed to; it is carried for the link, so it is hidden.</remarks>
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
