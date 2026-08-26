using System;
using System.Linq;
using System.Reflection;
using DM.Web.API.Shared.Authentication;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// An action that writes says who is allowed to.
/// </summary>
/// <remarks>
/// Authorization in this application is opt-in: a controller method with no
/// attribute on it and none on its class answers whoever asks, guest included.
/// Nothing about that is visible — the action runs, the write lands, the response
/// is a normal one — and the identity it acts on is the guest identity, so what
/// the write records is not even wrong, it is nobody.
///
/// Which makes it exactly the defect a review cannot be relied on to catch: the
/// missing line is invisible, and the reviewer is looking at the lines that are
/// there. The list of exceptions below is the whole surface a stranger may write
/// to, and it is short enough to read in one sitting — which is the point of
/// writing it down rather than discovering it.
/// </remarks>
public class AuthorizedActionsShould
{
    /// <summary>
    /// Writes a stranger is allowed to make, and why each of them has to be one.
    /// </summary>
    /// <remarks>
    /// Every entry is a door into the application from outside a session, so the
    /// list is stated here rather than inferred from an attribute: an attribute
    /// spelling "anonymous" is a decision one edit away from being made by accident,
    /// while a name added to this list is a decision somebody had to type out.
    /// </remarks>
    private static readonly string[] OpenToStrangers =
    [
        // There is no session yet — these are the ways one begins.
        "AuthenticationController.Login",
        // The second half of a login, and by construction there is no session at
        // this point: the password step of an account with a factor creates none.
        // What stands in for one is the short-lived challenge cookie, which the
        // action reads and which is worth nothing without a code to go with it.
        "AuthenticationController.CompleteTwoFactorLogin",
        "RegistrationController.Register",
        "RegistrationController.Activate",
        "RegistrationController.ResendActivation",
        "RecoveryController.RequestRecovery",
        "RecoveryController.CompletePasswordReset",
        // Asked from the registration form, before there is anybody to ask as. They
        // write nothing; they are POSTs because the value being checked is a login
        // and an address, and those do not belong in a query string or a log.
        "AvailabilityController.CheckEmail",
        "AvailabilityController.CheckUsername",
        // Reaching support has to be possible when reaching an account is not.
        "TicketIntakeController.CreateTicketIntake",
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
        var actions = MutatingActions();

        actions.Should().HaveCountGreaterThan(50,
            "the reflection has to find the write surface, and a walk that finds none " +
            "passes on anything");

        actions
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
    /// <remarks>
    /// Both directions. An entry naming an action that no longer exists reads like a
    /// door that is still open, and an entry naming one that has since been given an
    /// attribute quietly excuses the next action to lose its own.
    /// </remarks>
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
