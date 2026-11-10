namespace DM.Domain.Account.Features.Security;

/// <summary>
/// What a sealed value is for.
/// </summary>
/// <remarks>
/// One key seals every value a client carries, so the purpose is what keeps a value
/// sealed for one reader from opening for another.
/// </remarks>
public enum SealPurpose
{
    // No member is zero: a purpose left unset is refused, not read as the first one.
    SessionToken = 1,
    TwoFactorChallenge = 2
}
