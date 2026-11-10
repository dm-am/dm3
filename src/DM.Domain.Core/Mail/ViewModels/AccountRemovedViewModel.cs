namespace DM.Domain.Core.Mail.ViewModels;

/// <remarks>
/// Points at the support form: only moderation brings a removed account back.
/// </remarks>
public record AccountRemovedViewModel(string SupportUrl);
