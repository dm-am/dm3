using System;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Web.API.Features.Account;
using DM.Web.API.Features.Account.Credentials;
using DM.Web.API.Features.Account.Recovery;
using DM.Web.API.Features.Account.Registration;
using DM.Web.API.Features.Account.TwoFactor;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using DomainApprovalInfo = DM.Domain.Account.Features.UsernameChange.UsernameChangeApprovalInfo;

namespace DM.Web.API.Tests.Features.Account;

/// <summary>
/// The answer to a request that mailed a link says how long the link lives, in the hours the settings
/// give it: the receipt on the screen takes the number from here.
/// </summary>
/// <remarks>None of the hours is a default, so an answer that counted its own would show.</remarks>
public class MailedLinkAnswerShould : UnitTestBase
{
    private static readonly TokenConfiguration Tokens = new()
    {
        PasswordResetTokenLifetimeHours = 31,
        ActivationTokenLifetimeHours = 32,
        EmailChangeTokenLifetimeHours = 33,
        TwoFactorRemovalTokenLifetimeHours = 34,
        UsernameChangeApprovalTokenLifetimeHours = 35
    };

    private static RecoveryApiService Recovery(RecoveryResult result)
    {
        var recovery = Mock<IRecoveryService>();
        recovery.Recover(Arg.Any<string>()).Returns(result);
        return new RecoveryApiService(
            recovery, Mock<IPasswordChangeService>(), new UserMapper(), Options.Create(Tokens));
    }

    [Theory]
    [InlineData(RecoveryResult.PasswordReset, 31)]
    [InlineData(RecoveryResult.ActivationResent, 32)]
    [InlineData(RecoveryResult.NotFound, null)]
    public async Task NameTheLifetimeOfTheRecoveryLetter(RecoveryResult result, int? hours) =>
        (await Recovery(result).Recover(new RecoveryRequest { Email = "reader@dm.am" }))
            .LinkLifetimeHours.Should().Be(hours, "no letter, no link to name");

    [Fact]
    public async Task NameTheLifetimeOfTheActivationLinkInTheCreatedAnswer()
    {
        var registration = Mock<IRegistrationApiService>();
        registration.Register(Arg.Any<RegistrationRequest>()).Returns(new MailedLink { LinkLifetimeHours = 32 });
        var controller = new RegistrationController(
            registration, Mock<IActivationApiService>(), NullLogger<RegistrationController>.Instance);

        var answer = await controller.Register(new RegistrationRequest
        {
            Email = "reader@dm.am",
            Password = "a-longer-secret",
            AcceptedRules = true
        });

        var created = answer.Should().BeOfType<ObjectResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Value.Should().BeOfType<Envelope<MailedLink>>()
            .Which.Resource.LinkLifetimeHours.Should().Be(32);
    }

    [Fact]
    public async Task NameTheLifetimeOfTheActivationLink()
    {
        var service = new RegistrationApiService(
            Mock<IRegistrationService>(), new RegistrationMapper(), Options.Create(Tokens));

        (await service.Register(new RegistrationRequest { Email = "reader@dm.am", Password = "a-longer-secret" }))
            .LinkLifetimeHours.Should().Be(32);
    }

    private static CredentialsApiService Credentials(IUsernameChangeService? usernameChanges = null)
    {
        var identity = Mock<IIdentityProvider>();
        identity.Current.Returns(Identities.User(Guid.NewGuid(), "reader"));
        return new CredentialsApiService(
            Mock<IPasswordChangeService>(),
            Mock<IEmailChangeService>(),
            usernameChanges ?? Mock<IUsernameChangeService>(),
            identity,
            new CredentialsMapper(),
            new UserMapper(),
            Options.Create(Tokens));
    }

    [Fact]
    public async Task NameTheLifetimeOfTheEmailChangeLink() =>
        (await Credentials().RequestEmailChange(new EmailChangeRequest { Email = "next@dm.am", Password = "secret" }))
            .LinkLifetimeHours.Should().Be(33);

    [Fact]
    public async Task NameTheLifetimeOfAnApprovalLinkOnTheExpiredPage()
    {
        var usernameChanges = Mock<IUsernameChangeService>();
        usernameChanges.GetApprovalInfoAsync(Arg.Any<Guid>()).Returns(DomainApprovalInfo.Expired());

        var info = await Credentials(usernameChanges).GetUsernameChangeApprovalAsync(Guid.NewGuid());

        info!.Status.Should().Be("expired");
        info.LinkLifetimeHours.Should().Be(35);
    }

    [Fact]
    public async Task NameTheLifetimeOfTheRemovalLink()
    {
        var service = new TwoFactorApiService(
            Mock<ITwoFactorService>(), Mock<ITwoFactorRemovalService>(), Options.Create(Tokens));

        (await service.RequestRemoval(new TwoFactorRemovalRequest { Email = "reader@dm.am" }))
            .LinkLifetimeHours.Should().Be(34, "the same hours for every address: the answer must not tell them apart");
    }
}
