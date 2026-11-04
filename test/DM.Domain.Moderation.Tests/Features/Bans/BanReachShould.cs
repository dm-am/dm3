using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Bans;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Bans;

/// <summary>
/// Who may ban whom: a rank strictly below the caller's and
/// never an administrator, and a voluntary ban only on oneself.
/// </summary>
public class BanReachShould : BanServiceTestBase
{
    /// <summary>
    /// The voluntary branch bans its author and nobody else.
    /// </summary>
    /// <remarks>
    /// It is the one branch that skips the senior-moderator gate, the
    /// administrator exemption and the strictly-lower-role rule at once, so a
    /// caller who could set the flag could ban anyone - and the ban would be filed
    /// under the victim's own name.
    /// </remarks>
    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Admin)]
    public async Task RefuseAVoluntaryBanAimedAtSomebodyElse(UserRole targetRole)
    {
        SetCurrentUser(UserRole.RegularUser);
        ArrangeTarget(targetRole);

        var act = () => service.CreateBan(new CreateBan { Username = "Target", IsVoluntary = true });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Добровольный бан можно наложить только на себя");
        await banRepository.DidNotReceive().Create(Arg.Any<CreateBanEntity>(), Arg.Any<CancellationToken>());
    }

    private void ArrangeTarget(UserRole targetRole)
    {
        userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = targetUserId, Username = "Target", Role = targetRole });
        banRepository.GetActiveBan(targetUserId, Arg.Any<CancellationToken>()).Returns((Ban?)null);
        banRepository.Create(Arg.Any<CreateBanEntity>(), Arg.Any<CancellationToken>()).Returns(new Ban());
    }

    /// <summary>
    /// The same arrangement, with the caller as the target: a genuine self-ban.
    /// </summary>
    private void ArrangeSelf(UserRole role)
    {
        SetCurrentUser(role);
        userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = moderatorUserId, Username = "Target", Role = role });
        banRepository.GetActiveBan(moderatorUserId, Arg.Any<CancellationToken>()).Returns((Ban?)null);
        banRepository.Create(Arg.Any<CreateBanEntity>(), Arg.Any<CancellationToken>()).Returns(new Ban());
    }

    private static CreateBan BanRequest() => new()
    {
        Username = "Target",
        DurationHours = 24,
        Comment = "Spam",
        IsVoluntary = false
    };

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public async Task BanAUserWhoseRoleIsBelowYours(UserRole targetRole)
    {
        ArrangeTarget(targetRole);

        var act = () => service.CreateBan(BanRequest());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefuseToBanAnEqualRole()
    {
        // Otherwise two senior moderators can ban each other in turn.
        ArrangeTarget(UserRole.SeniorModerator);

        var act = () => service.CreateBan(BanRequest());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RefuseToBanAnAdministrator()
    {
        // An administrator is a site owner: no in-app authority over one, and a
        // senior moderator locking the only admin out would be unrecoverable.
        SetCurrentUser(UserRole.Admin);
        ArrangeTarget(UserRole.Admin);

        var act = () => service.CreateBan(BanRequest());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StillAllowAVoluntarySelfBan()
    {
        // The role comparison must not catch the one legitimate self-ban.
        ArrangeSelf(UserRole.SeniorModerator);
        var request = BanRequest();
        request.IsVoluntary = true;

        var act = () => service.CreateBan(request);

        await act.Should().NotThrowAsync();
    }
}
