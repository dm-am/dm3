namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// What one purge of expired sessions removed.
/// </summary>
/// <param name="SessionsRemoved">Expired sessions deleted by the pass.</param>
public record SessionPurgeResult(long SessionsRemoved);
