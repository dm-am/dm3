using System;

namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for the letter that says the second factor is about to be taken
/// off, and how to stop it
/// </summary>
/// <paramref name="Username">
/// Username
/// </paramref>
/// <paramref name="DueUtc">
/// When the factor comes off
/// </paramref>
/// <paramref name="CancellationLinkUrl">
/// Link that calls the removal off
/// </paramref>
public record TwoFactorRemovalScheduledViewModel(
    string Username,
    DateTimeOffset DueUtc,
    string CancellationLinkUrl);
