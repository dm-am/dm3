namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// The letter that says the account is gone
/// </summary>
/// <remarks>
/// Points at the support form: only moderation brings a removed account back.
/// </remarks>
/// <param name="SupportUrl">Address of the support page</param>
public record AccountRemovedViewModel(string SupportUrl);
