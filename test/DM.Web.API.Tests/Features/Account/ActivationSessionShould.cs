using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Authentication.Credentials;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;
using ApiActivationRequest = DM.Web.API.Features.Account.Registration.ActivationRequest;
using ServiceActivationRequest = DM.Domain.Account.Features.Registration.ActivationRequest;

namespace DM.Web.API.Tests.Features.Account;

/// <summary>
/// Activation hands back a reader who is actually signed in, or nothing at all.
/// </summary>
/// <remarks>
/// The last step of activation signs the reader in without a password, and that
/// path refuses an account which already carries a second factor. A name chosen
/// seconds ago cannot carry one, so the refusal is a broken rule rather than an
/// answer - but the answer went out unread, and what left the server was a guest
/// wearing the shape of a user under a screen saying the account was ready. The
/// reader was signed out with nothing on the page saying so.
/// </remarks>
public class ActivationSessionShould : UnitTestBase
{
    private static readonly Guid Token = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IActivationService _activationService = Mock<IActivationService>();
    private readonly IWebAuthenticationService _authentication = Mock<IWebAuthenticationService>();
    private readonly DM.Web.API.Features.Account.Registration.ActivationApiService _service;

    public ActivationSessionShould()
    {
        _activationService
            .Activate(Arg.Any<ServiceActivationRequest>())
            .Returns(UserId);

        _service = new DM.Web.API.Features.Account.Registration.ActivationApiService(
            _activationService, _authentication, new UserMapper(Mock<IImgproxyUrlBuilder>()));
    }

    [Fact]
    public async Task RefuseToAnswerWhenTheSessionWasNotMinted()
    {
        AutoLoginReturns(AuthenticatedUser.Guest, AuthenticationError.Forbidden);

        var broken = await Assert.ThrowsAsync<InvalidOperationException>(Activate);

        broken.Message.Should().Contain(UserId.ToString()).And.Contain("Forbidden");
    }

    [Fact]
    public async Task AnswerWithTheReaderWhenTheSessionWasMinted()
    {
        AutoLoginReturns(
            new AuthenticatedUser { UserId = UserId, Username = "lex", Role = UserRole.RegularUser },
            AuthenticationError.NoError);

        var answer = await Activate();

        answer.Resource.Username.Should().Be("lex");
    }

    private Task<Envelope<User>> Activate() =>
        _service.Activate(Token, new ApiActivationRequest { Username = "lex" }, new DefaultHttpContext());

    private void AutoLoginReturns(AuthenticatedUser user, AuthenticationError error)
    {
        var identity = Mock<IIdentity>();
        identity.User.Returns(user);
        identity.Error.Returns(error);
        _authentication
            .Authenticate(Arg.Any<AuthCredentials>(), Arg.Any<HttpContext>())
            .Returns(identity);
    }
}
