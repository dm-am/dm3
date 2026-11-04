using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Invitations;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Invitations;
using DM.Testing;
using DM.Web.API.Features.Personal.Invitations;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

/// <summary>
/// An invitation the account page presses that is in neither list was settled elsewhere or never was.
/// </summary>
public class PersonalInvitationApiServiceShould : UnitTestBase
{
    private readonly IGameInvitationService _games = Mock<IGameInvitationService>();
    private readonly IBlogInvitationService _blogs = Mock<IBlogInvitationService>();
    private readonly PersonalInvitationApiService _service;

    public PersonalInvitationApiServiceShould()
    {
        _games.GetUserInvitations(Arg.Any<CancellationToken>()).Returns(Array.Empty<GameInvitation>());
        _blogs.GetUserPendingInvitations().Returns(Array.Empty<BlogInvitation>());
        _service = new PersonalInvitationApiService(_games, _blogs);
    }

    public static TheoryData<string> Presses => new() { "accept", "reject" };

    [Theory]
    [MemberData(nameof(Presses))]
    public async Task NameBothReasonsForAnInvitationInNeitherList(string press)
    {
        var tokenId = Guid.NewGuid();

        var act = () => press == "accept"
            ? _service.AcceptInvitation(tokenId)
            : _service.RejectInvitation(tokenId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound
                        && e.Message == "Приглашение не найдено или уже обработано");
    }
}
