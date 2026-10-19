using System;
using System.Linq;
using System.Reflection;
using DM.Web.API.Shared.Authentication;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// An action that writes says who is allowed to.
/// </summary>
/// <remarks>
/// Authorization is opt-in: an action with no attribute answers whoever asks. The exceptions below are the
/// whole surface a stranger may write to.
/// </remarks>
public class AuthorizedActionsShould
{
    /// <summary>
    /// Writes a stranger is allowed to make, and why each of them has to be one.
    /// </summary>
    /// <remarks>Each entry is a door from outside a session, so it is typed out here, not inferred from an attribute.</remarks>
    private static readonly string[] OpenToStrangers =
    [
        // There is no session yet — these are the ways one begins.
        "AuthenticationController.Login",
        // The second half of a login has no session yet; the short-lived challenge cookie stands for one.
        "AuthenticationController.CompleteTwoFactorLogin",
        "RegistrationController.Register",
        "RegistrationController.Activate",
        "RecoveryController.RequestRecovery",
        "RecoveryController.CompletePasswordReset",
        // Asked from the registration form, before there is anybody to ask as. They
        // write nothing; they are POSTs because the value being checked is a login
        // and an address, and those do not belong in a query string or a log.
        "AvailabilityController.CheckEmail",
        "AvailabilityController.CheckUsername",
        // Reaching support has to be possible when reaching an account is not.
        "TicketIntakeController.CreateTicketIntake",
        // The guest's answer in his own ticket. The tracking token in the header is
        // the credential, as on the tracking page, and an unknown one writes nothing.
        "TicketIntakeController.CreateTrackedTicketComment",
        // Called by another machine, which carries a signature rather than a session.
        "WebhookController.HandleWebhook",
    ];

    private static (string Name, MethodInfo Method)[] MutatingActions() => ApiSurface.MutatingActions()
        .Select(method => (ApiSurface.Named(method), method))
        .ToArray();

    private static bool Guarded(MethodInfo method) =>
        Guards(method) || Guards(method.DeclaringType!);

    private static bool Guards(MemberInfo member) =>
        member.GetCustomAttributes().Any(attribute =>
            attribute is AuthenticationRequiredAttribute or RequireRoleAttribute);

    [Fact]
    public void NameWhoMayWrite()
    {
        MutatingActions()
            .Where(action => !Guarded(action.Method))
            .Select(action => action.Name)
            .Where(name => !OpenToStrangers.Contains(name, StringComparer.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Should().BeEmpty(
                "authorization here is opt-in, so an action with no attribute on it and none " +
                "on its class writes for whoever asks, under the guest identity");
    }

    /// <summary>
    /// A list of exceptions rots the moment nobody checks it.
    /// </summary>
    /// <remarks>Both directions: an entry for a missing action, and one for an action that gained an attribute.</remarks>
    [Fact]
    public void KeepTheListOfOpenDoorsHonest()
    {
        var actions = MutatingActions();

        foreach (var name in OpenToStrangers)
        {
            var action = actions.FirstOrDefault(a => a.Name == name);

            action.Name.Should().NotBeNull($"{name} is excused and does not exist");
            Guarded(action.Method).Should().BeFalse(
                $"{name} carries an attribute now, so excusing it hides the next action " +
                "that loses one");
        }
    }
}
