using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Warnings;

/// <summary>
/// The deletion of the picture a warning on a profile pinned.
/// </summary>
public class ProfilePictureDeletionShould : WarningServiceTestBase
{
    /// <summary>
    /// The picture of a warning on a profile is deleted in the name of the
    /// moderator who asked and at the moment he asked: that mark is what takes it
    /// out of both logs and hands the file to the sweeper.
    /// </summary>
    [Fact]
    public async Task DeleteTheProfilePictureInTheNameOfTheModerator()
    {
        warningRepository.GetTargetUserId(warningId, Arg.Any<CancellationToken>()).Returns(targetUserId);
        warningRepository
            .DeleteProfilePicture(warningId, moderatorUserId, now, Arg.Any<CancellationToken>())
            .Returns(true);

        await service.DeleteProfilePicture(warningId);

        await warningRepository.Received(1).DeleteProfilePicture(
            warningId, moderatorUserId, now, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A reader is refused before anything is written: the controller asks for
    /// the role as well, and this is the rule for any other caller of the service.
    /// </summary>
    [Fact]
    public async Task RefuseAReaderTheDeletionOfAProfilePicture()
    {
        Authenticate(Guid.NewGuid(), UserRole.RegularUser, "Reader");

        var act = () => service.DeleteProfilePicture(warningId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await warningRepository.DidNotReceive().DeleteProfilePicture(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A moderator does not take the picture out of a warning issued to himself,
    /// before his promotion or by a higher rank: a party does not rule on his own
    /// case, as with tickets, and nothing is marked.
    /// </summary>
    /// <remarks>
    /// The picture is his own avatar. Marked in his name, the deletion reads as
    /// the owner's own doing to the log and to the orphan sweeper, so the picture
    /// stayed in the log while the request succeeded, and his current avatar, if
    /// it was still that file, left his profile.
    /// </remarks>
    [Fact]
    public async Task RefuseAModeratorThePictureOfAWarningIssuedToHimself()
    {
        warningRepository.GetTargetUserId(warningId, Arg.Any<CancellationToken>()).Returns(moderatorUserId);

        var act = () => service.DeleteProfilePicture(warningId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Убрать картинку из лога о вас может только другой модератор");
        await warningRepository.DidNotReceive().DeleteProfilePicture(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A warning nobody issued is answered with 404, the code the change of
    /// points gives for it, before anything is written.
    /// </summary>
    [Fact]
    public async Task AnswerTheDeletionOfAPictureOfAMissingWarningWithNotFound()
    {
        warningRepository.GetTargetUserId(warningId, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var act = () => service.DeleteProfilePicture(warningId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await warningRepository.DidNotReceive().DeleteProfilePicture(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// So is a warning that is gone by the time of the write.
    /// </summary>
    [Fact]
    public async Task AnswerTheDeletionOfAPictureOfAWarningGoneByTheWriteWithNotFound()
    {
        warningRepository.GetTargetUserId(warningId, Arg.Any<CancellationToken>()).Returns(targetUserId);
        warningRepository
            .DeleteProfilePicture(warningId, moderatorUserId, now, Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => service.DeleteProfilePicture(warningId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }
}
