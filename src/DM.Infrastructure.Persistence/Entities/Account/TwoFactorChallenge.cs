using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for the state between a proven password and a session.
/// </summary>
/// <remarks>
/// A stream with a term: a row per unfinished login, lives five minutes, swept
/// after a day. The day is slack for the case where the deletion at the end of a
/// login did not happen, not the lifetime of the challenge.
///
/// It is not a session and cannot be mistaken for one: no code path turns this
/// row into an identity, so nothing that authorizes anything has to remember it.
/// </remarks>
[Table("TwoFactorChallenges")]
public class TwoFactorChallenge
{
    /// <summary>
    /// Challenge identifier, and the value the short-lived cookie carries
    /// </summary>
    public Guid ChallengeId { get; set; }

    /// <summary>
    /// Whose login is unfinished
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The account identifier the password step counted attempts under, so that
    /// a wrong code lands in the same counter rather than in one of its own
    /// </summary>
    public string Account { get; set; } = null!;

    /// <summary>
    /// When the challenge was created. The retention pass measures from here
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When it stops being usable
    /// </summary>
    public DateTimeOffset ExpiresUtc { get; set; }

    /// <summary>
    /// "Remember me" as it was ticked on the first step
    /// </summary>
    public bool Persistent { get; set; }

    /// <summary>
    /// How many codes have already failed against this challenge
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Address the session will be created from
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent the session will be created from
    /// </summary>
    public string? UserAgent { get; set; }
}
