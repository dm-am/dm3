namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Factory for a password salt
/// </summary>
internal interface ISaltFactory
{
    /// <summary>
    /// Generates password salt of given length
    /// </summary>
    /// <param name="saltLength">Salt length</param>
    /// <returns>Random string for a password salt</returns>
    string Create(int saltLength);
}
