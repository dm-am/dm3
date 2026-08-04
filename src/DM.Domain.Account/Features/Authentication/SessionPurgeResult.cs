namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// What one purge of expired sessions removed.
/// </summary>
/// <param name="UsersTouched">Accounts at least one expired session was dropped from.</param>
/// <param name="EmptyDocumentsRemoved">Session documents deleted for holding no session at all.</param>
public record SessionPurgeResult(long UsersTouched, long EmptyDocumentsRemoved);
