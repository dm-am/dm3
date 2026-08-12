using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DM.Web.API.Shared.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// Which endpoints spend a budget, and which budget.
/// </summary>
/// <remarks>
/// The limit is one attribute. Deleting it, or writing a new controller by
/// copying a neighbour and dropping it, removes the budget and breaks nothing:
/// the integration suite starts the host with RateLimiting:Enabled false and the
/// CI job passes DM_RateLimiting__Enabled false, because a limiter that counts
/// turns a suite of several hundred requests into a source of 429s. So every
/// endpoint under a budget is only ever exercised without one, and the login
/// endpoint could go a release with no limit on password guessing while every
/// test stayed green.
///
/// The registry below is what makes the binding reviewable. Adding a line is the
/// claim "this endpoint needs a budget of its own", removing one is the claim
/// that it no longer does, and neither can happen without a diff somebody reads.
/// It is asserted in both directions: an endpoint that lost its attribute turns
/// the first test red, one that gained a policy nobody wrote down turns the
/// second.
///
/// Read off the attributes rather than over the wire, for the reason
/// RateLimitPartitioningShould states next door: the limiter is framework code,
/// and what this project decides is which policy goes where.
/// </remarks>
public class RateLimitPolicyBindingShould
{
    /// <summary>The namespace every controller of the host lives under.</summary>
    private const string FeaturePrefix = "DM.Web.API.Features.";

    /// <summary>
    /// Every endpoint under a policy: a controller when the whole of it is
    /// limited, a controller and an action when only that action is.
    /// </summary>
    /// <remarks>
    /// Named by namespace and type rather than by the type alone, because the
    /// short name is not unique - there are two ProfileControllers in the tree.
    /// Moving or renaming a controller therefore turns this red, which is what
    /// naming an endpoint at all costs.
    /// </remarks>
    private static readonly Dictionary<string, string> Registry = new(StringComparer.Ordinal)
    {
        ["Account.Authentication.AuthenticationController"] = RateLimitPolicies.Auth,
        ["Account.Availability.AvailabilityController.CheckEmail"] = RateLimitPolicies.EmailCheck,
        ["Account.Availability.AvailabilityController.CheckUsername"] = RateLimitPolicies.UsernameCheck,
        ["Account.Credentials.CredentialsController"] = RateLimitPolicies.Auth,
        ["Account.Deactivation.DeactivationController"] = RateLimitPolicies.Auth,
        ["Account.Recovery.RecoveryController"] = RateLimitPolicies.Auth,
        ["Account.Registration.RegistrationController"] = RateLimitPolicies.Auth,
        ["Blog.Blacklists.BlogBlacklistController"] = RateLimitPolicies.Default,
        ["Game.Blacklists.GameBlacklistController"] = RateLimitPolicies.Default,
        ["General.Tickets.TicketIntakeController.CreateTicketIntake"] = RateLimitPolicies.Auth,
        ["General.Tickets.TicketIntakeController.TrackTicketIntake"] = RateLimitPolicies.Auth,
        ["General.Upload.UploadController.DirectUpload"] = RateLimitPolicies.Uploads,
        ["Messaging.Chats.ChatController.CanStartChat"] = RateLimitPolicies.Sliding,
        ["Personal.Blacklists.BlacklistController"] = RateLimitPolicies.Default,
        ["Personal.Invitations.InvitationController"] = RateLimitPolicies.Default,
        ["Personal.Notepads.NotepadController"] = RateLimitPolicies.Default,
        ["Personal.Notifications.NotificationController"] = RateLimitPolicies.Default,
        ["Personal.Preferences.PreferencesController"] = RateLimitPolicies.Default,
        ["Personal.ProfileNotes.UserProfileNoteController"] = RateLimitPolicies.Default,
        ["Personal.Profiles.ProfileController"] = RateLimitPolicies.Default,
        ["Personal.Subscriptions.SubscriptionController"] = RateLimitPolicies.Default,
        ["Personal.Webhooks.WebhookController.HandleWebhook"] = RateLimitPolicies.Sliding,
        ["General.Search.ForumSearchController.SearchForum"] = RateLimitPolicies.Sliding,
        ["General.Search.MessageSearchController.SearchMessages"] = RateLimitPolicies.Sliding,
    };

    /// <summary>Every [EnableRateLimiting] of the host, named the same way.</summary>
    private static Dictionary<string, string> Applied()
    {
        var applied = new Dictionary<string, string>(StringComparer.Ordinal);

        var controllers = typeof(Startup).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var controller in controllers)
        {
            var onController = controller.GetCustomAttribute<EnableRateLimitingAttribute>();
            if (onController?.PolicyName != null)
            {
                applied[Named(controller)] = onController.PolicyName;
            }

            var actions = controller.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var action in actions)
            {
                var onAction = action.GetCustomAttribute<EnableRateLimitingAttribute>();
                if (onAction?.PolicyName != null)
                {
                    applied[$"{Named(controller)}.{action.Name}"] = onAction.PolicyName;
                }
            }
        }

        return applied;
    }

    /// <summary>A controller, spelled the way the registry spells it.</summary>
    private static string Named(Type controller) =>
        controller.FullName!.StartsWith(FeaturePrefix, StringComparison.Ordinal)
            ? controller.FullName![FeaturePrefix.Length..]
            : controller.FullName!;

    [Fact]
    public void LimitEveryEndpointTheRegistryNames()
    {
        Applied().Should().Contain(Registry,
            "an endpoint that quietly lost its attribute spends nobody's budget, and " +
            "the suites that would have noticed run with the limiter switched off");
    }

    [Fact]
    public void NameEveryEndpointItLimits()
    {
        var applied = Applied();
        applied.Should().NotBeEmpty("the API limits its credential endpoints at least");

        Registry.Should().Contain(applied,
            "a policy chosen while copying a neighbouring controller is a choice nobody " +
            "made, and this is the list where it would have been read");
    }

    [Fact]
    public void LeaveNoPolicyNobodyAsksFor()
    {
        var declared = typeof(RateLimitPolicies)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();
        declared.Should().NotBeEmpty("the policy names are declared as constants");

        Applied().Values.Distinct().Should().BeEquivalentTo(declared,
            "both halves of the handshake go through these constants, and that is the " +
            "whole of what makes the pairing a compile-time one: a name outside them is " +
            "a policy AddDmRateLimiting never registered, and a constant nothing asks " +
            "for is a limiter registered for no endpoint");
    }
}
