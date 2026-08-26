using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DM.Web.API.Shared.RateLimiting;
using AwesomeAssertions;
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
        // The second factor is counted per endpoint because it is counted per two
        // different things. What the owner does to his own factor carries a
        // session and spends his own budget; the mailed removal path carries
        // nothing and spends the address budget of the credential surface, which
        // is the only budget an anonymous caller can be held to.
        ["Account.TwoFactor.TwoFactorController.CancelTwoFactorRemoval"] = RateLimitPolicies.Auth,
        ["Account.TwoFactor.TwoFactorController.ClearTwoFactorForColleague"] = RateLimitPolicies.TwoFactor,
        ["Account.TwoFactor.TwoFactorController.ConfirmTwoFactor"] = RateLimitPolicies.TwoFactor,
        ["Account.TwoFactor.TwoFactorController.DisableTwoFactor"] = RateLimitPolicies.TwoFactor,
        ["Account.TwoFactor.TwoFactorController.GetTwoFactorStatus"] = RateLimitPolicies.TwoFactor,
        ["Account.TwoFactor.TwoFactorController.ReissueRecoveryCodes"] = RateLimitPolicies.TwoFactor,
        ["Account.TwoFactor.TwoFactorController.RequestTwoFactorRemoval"] = RateLimitPolicies.Auth,
        ["Account.TwoFactor.TwoFactorController.ScheduleTwoFactorRemoval"] = RateLimitPolicies.Auth,
        ["Account.TwoFactor.TwoFactorController.SetupTwoFactor"] = RateLimitPolicies.TwoFactor,
        ["Blog.Blacklists.BlogBlacklistController"] = RateLimitPolicies.Default,
        ["Blog.Blogs.BlogController.DeleteBlog"] = RateLimitPolicies.Default,
        ["Blog.Blogs.BlogController.DeleteRubric"] = RateLimitPolicies.Default,
        ["Blog.Blogs.BlogController.PatchBlog"] = RateLimitPolicies.Default,
        ["Blog.Blogs.BlogController.PatchRubric"] = RateLimitPolicies.Default,
        ["Blog.Blogs.BlogController.PostBlog"] = RateLimitPolicies.Default,
        ["Blog.Blogs.BlogController.PostBlogPremoderation"] = RateLimitPolicies.Default,
        ["Blog.Blogs.BlogController.PostBlogStatus"] = RateLimitPolicies.Default,
        ["Blog.Blogs.BlogController.PostRubric"] = RateLimitPolicies.Default,
        ["Blog.Blogs.BlogController.PutRubricsOrder"] = RateLimitPolicies.Default,
        ["Blog.Notepads.BlogNotepadController"] = RateLimitPolicies.Default,
        ["Game.Blacklists.GameBlacklistController"] = RateLimitPolicies.Default,
        ["Game.Games.GameController.DeleteGame"] = RateLimitPolicies.Default,
        ["Game.Games.GameController.PatchGameDetails"] = RateLimitPolicies.Default,
        ["Game.Games.GameController.PostGame"] = RateLimitPolicies.Default,
        ["Game.Games.GameController.PostGamePremoderation"] = RateLimitPolicies.Default,
        ["Game.Games.GameController.PostGameStatus"] = RateLimitPolicies.Default,
        ["Game.Games.GameController.PostResetRecruitmentDate"] = RateLimitPolicies.Default,
        ["Game.Notepads.GameNotepadController"] = RateLimitPolicies.Default,
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

        var controllers = ApiSurface.Controllers();

        foreach (var controller in controllers)
        {
            var onController = controller.GetCustomAttribute<EnableRateLimitingAttribute>();
            if (onController?.PolicyName != null)
            {
                applied[Named(controller)] = onController.PolicyName;
            }

            var actions = controller.GetMethods(ApiSurface.ActionBinding);
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

    /// <summary>
    /// A name an endpoint asks for and a limiter the host registers are two
    /// separate lists, and only the second one counts anything.
    /// </summary>
    /// <remarks>
    /// The third side of the triangle the two tests above draw. A constant that
    /// no policy record declares is not a compile error and not a startup error:
    /// it throws on the first request to the endpoint that asks for it, which in
    /// a suite running with the limiter switched off is the first request in
    /// production.
    /// </remarks>
    [Fact]
    public void RegisterALimiterForEveryPolicyAnEndpointAsksFor()
    {
        RateLimitingExtensions.Policies.Select(policy => policy.Name)
            .Should().Contain(Applied().Values.Distinct(),
                "an endpoint asking by name for a policy the host never registered " +
                "answers 500, and it does so only in a deployment that counts");
    }

    /// <summary>
    /// Whose minute the second factor's own settings spend.
    /// </summary>
    /// <remarks>
    /// Asserted because the failure is invisible from every direction. The whole
    /// controller sat in the credential budget - five requests per address per
    /// minute, the number sized for online password guessing - and switching a
    /// factor on costs four requests in one sitting, after the sign-in that got
    /// the person there has spent one of the five. One mistyped confirmation code
    /// answered 429; behind carrier-grade NAT, where one address is a
    /// neighbourhood, so did the first request. The integration suite could not
    /// see any of it: it runs with the limiter switched off, which is why this is
    /// asserted on the table rather than by counting to 429.
    /// </remarks>
    [Fact]
    public void CountTheFactorsOwnSettingsPerAccountAndNotPerAddress()
    {
        Declared(RateLimitPolicies.TwoFactor).Partition
            .Should().Be(RateLimitingExtensions.Partition.AccountThenAddress,
                "every one of these endpoints carries a session, and the person behind " +
                "an office or a carrier address is one account however many neighbours " +
                "share the address with him");

        Declared(RateLimitPolicies.TwoFactor).PermitLimit
            .Should().BeGreaterThan(Declared(RateLimitPolicies.Auth).PermitLimit,
                "switching the factor on is four requests in one sitting and a mistyped " +
                "code has to cost a refusal that says the code was wrong, not a 429 that " +
                "says nothing");
    }

    /// <summary>
    /// The mailed removal path stays on the address budget.
    /// </summary>
    /// <remarks>
    /// It is reachable without a session, so there is no account to hold it to,
    /// and it is a guessing surface plus a way to make the site send letters.
    /// Moving it into the per-account policy next door would count it per address
    /// anyway - the fallback for guests - but at the laxer number.
    /// </remarks>
    [Fact]
    public void CountTheMailedRemovalPathPerAddress()
    {
        var mailed = Applied()
            .Where(pair => pair.Key.Contains("TwoFactorRemoval", StringComparison.Ordinal))
            .Select(pair => pair.Value)
            .Distinct();

        mailed.Should().NotBeEmpty("the mailed removal path exists");
        foreach (var policy in mailed)
        {
            Declared(policy).Partition.Should().Be(RateLimitingExtensions.Partition.Address,
                "an anonymous caller is known by nothing but the address it arrives from");
        }
    }

    /// <summary>The registered limiter behind a policy name.</summary>
    private static RateLimitingExtensions.Policy Declared(string name) =>
        RateLimitingExtensions.Policies.Single(policy => policy.Name == name);
}
