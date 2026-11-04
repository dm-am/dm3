using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DM.Web.API.Shared.RateLimiting;
using AwesomeAssertions;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// Which endpoints spend a budget, and which budget.
/// </summary>
/// <remarks>
/// The tests run with the limiter off, so every budgeted endpoint is exercised without one; the registry
/// makes the binding reviewable and is asserted both ways, off the attributes.
/// </remarks>
public class RateLimitPolicyBindingShould
{
    /// <summary>The namespace every controller of the host lives under.</summary>
    private const string FeaturePrefix = "DM.Web.API.Features.";

    /// <summary>
    /// Every endpoint under a policy: a controller when the whole of it is
    /// limited, a controller and an action when only that action is.
    /// </summary>
    /// <remarks>Named by namespace and type: there are two ProfileControllers.</remarks>
    private static readonly Dictionary<string, string> Registry = new(StringComparer.Ordinal)
    {
        ["Account.Authentication.AuthenticationController"] = RateLimitPolicies.Auth,
        ["Account.Availability.AvailabilityController.CheckEmail"] = RateLimitPolicies.EmailCheck,
        ["Account.Availability.AvailabilityController.CheckUsername"] = RateLimitPolicies.UsernameCheck,
        ["Account.Credentials.CredentialsController"] = RateLimitPolicies.Auth,
        ["Account.Recovery.RecoveryController"] = RateLimitPolicies.Auth,
        ["Account.Registration.RegistrationController"] = RateLimitPolicies.Auth,
        // The owner's own factor spends his budget; the mailed removal path spends the address budget.
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
        ["Game.Games.GameController.PutGameAttributeSchema"] = RateLimitPolicies.Default,
        ["Game.Notepads.GameNotepadController"] = RateLimitPolicies.Default,
        ["General.Tickets.TicketIntakeController.CreateTicketIntake"] = RateLimitPolicies.Auth,
        // The guest writes in his ticket by the token alone, so the budget is the
        // address budget of the tracking page, shared with reading it.
        ["General.Tickets.TicketIntakeController.CreateTrackedTicketComment"] = RateLimitPolicies.Auth,
        ["General.Tickets.TicketIntakeController.TrackTicketIntake"] = RateLimitPolicies.Auth,
        ["General.Upload.UploadController.DirectUpload"] = RateLimitPolicies.Uploads,
        ["General.Upload.UploadController.PatchUpload"] = RateLimitPolicies.Default,
        ["Personal.Blacklists.BlacklistController"] = RateLimitPolicies.Default,
        ["Personal.Invitations.InvitationController"] = RateLimitPolicies.Default,
        ["Personal.Notepads.NotepadController"] = RateLimitPolicies.Default,
        ["Personal.Notifications.NotificationController"] = RateLimitPolicies.Default,
        ["Personal.Preferences.PreferencesController"] = RateLimitPolicies.Default,
        ["Personal.ProfileNotes.UserProfileNoteController"] = RateLimitPolicies.Default,
        ["Personal.Profiles.ProfileController"] = RateLimitPolicies.Default,
        ["Personal.Subscriptions.SubscriptionController"] = RateLimitPolicies.Default,
        ["Personal.Webhooks.WebhookController.HandleWebhook"] = RateLimitPolicies.Sliding,
        ["Moderation.Tickets.TicketController.CreateTicketComment"] = RateLimitPolicies.Default,
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
    /// <remarks>A constant no policy declares throws on the first request to its endpoint.</remarks>
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
    /// <remarks>Switching a factor on costs four requests, more than the credential budget leaves after sign-in.</remarks>
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
    /// <remarks>Reachable without a session, so there is no account to hold it to.</remarks>
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
