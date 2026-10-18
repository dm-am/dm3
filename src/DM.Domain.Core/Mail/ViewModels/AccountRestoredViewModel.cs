namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// The letter that says the account answers again
/// </summary>
/// <param name="LoginUrl">Address of the site with the sign-in form open</param>
public record AccountRestoredViewModel(string LoginUrl);
