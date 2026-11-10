using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Domain.Account.Configuration;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// The hours reach the letter and the screen from the settings the host runs with, so the words carry
/// no number of their own; the client keeps one, for a screen shown before any answer named the hours.
/// The delay of a removal is still words, so its number is read out of the sources and held to the setting.
/// </remarks>
public class LinkLifetimeShould
{
    private static readonly TokenConfiguration Tokens = new();

    private const string Hours = @"действительна (?<n>\d+) час";

    private const string Days = @"через (?<n>\d+) дн";

    public static TheoryData<string> Words => new()
    {
        "src/DM.Web.Client/src/features/auth/ui/AccessRecoveryForm.vue",
        "src/DM.Web.Client/src/features/auth/ui/LoginForm.vue",
        "src/DM.Web.Client/src/features/auth/ui/RegistrationForm.vue",
        "src/DM.Web.Client/src/features/auth/ui/RegistrationSuccess.vue",
        "src/DM.Web.Client/src/features/auth/ui/TwoFactorRemovalForm.vue",
        "src/DM.Web.Client/src/features/auth/ui/LetterReceipt.vue",
        "src/DM.Web.Client/src/pages/account/AccountActivationPage.vue",
        "src/DM.Web.Client/src/pages/account/UsernameChangePage.vue",
        "src/DM.Web.Client/src/pages/account/sections/AccountSecuritySection.vue",
        "src/DM.Infrastructure.Mail/Templates/PasswordResetConfirmation.razor",
        "src/DM.Infrastructure.Mail/Templates/RegistrationConfirmation.razor",
        "src/DM.Infrastructure.Mail/Templates/UsernameChangeApproval.razor",
        "src/DM.Infrastructure.Mail/Templates/LinkLifetime.razor",
    };

    [Theory]
    [MemberData(nameof(Words))]
    public void TakeTheHoursFromTheServer(string file)
    {
        Regex.IsMatch(Read(file), Hours).Should().BeFalse(
            $"{file} says the hours the server answers with: a number of its own outlives the setting it copied");
    }

    /// <summary>
    /// The one number the client keeps is said before the resend of an activation letter, so it is
    /// the activation link's.
    /// </summary>
    [Fact]
    public void FallBackToTheActivationLifetime() =>
        ClientCopies.Number("features/auth/lib/linkLifetime.ts", "FALLBACK_LINK_LIFETIME_HOURS")
            .Should().Be(Tokens.ActivationTokenLifetimeHours,
                "the page offering a new activation link promises it before the server has named the hours");

    public static TheoryData<string, int> Delays => new()
    {
        { "src/DM.Web.Client/src/features/auth/ui/TwoFactorRemovalForm.vue", new TwoFactorConfiguration().RemovalDelayDays },
        { "src/DM.Web.Client/src/pages/account/TwoFactorRemovalPage.vue", new TwoFactorConfiguration().RemovalDelayDays },
        { "src/DM.Web.Client/src/features/support-ticket/model/ticketReasons.ts", new TwoFactorConfiguration().RemovalDelayDays },
    };

    [Theory]
    [MemberData(nameof(Delays))]
    public void PromiseTheDelayTheServerHolds(string file, int setting)
    {
        var said = Regex.Matches(Read(file), Days)
            .Select(match => int.Parse(match.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

        said.Should().NotBeEmpty($"{file} tells the reader when the factor goes");
        said.Should().AllBeEquivalentTo(setting,
            "RemovalDelayDays is the delay: a removal sooner than promised is a hole, a later one a support ticket");
    }

    [Fact]
    public void RunWithTheLifetimesTheDefaultsState()
    {
        Setting(nameof(TokenConfiguration.ActivationTokenLifetimeHours)).Should().Be(Tokens.ActivationTokenLifetimeHours,
            "the client's fallback is the default, so the deployed setting may not move away from it");
    }

    private static string Read(string file) => File.ReadAllText(Path.Combine(RepositoryLayout.Root, file));

    private static int Setting(string key) => ClientCopies.Setting(nameof(TokenConfiguration), key);
}
