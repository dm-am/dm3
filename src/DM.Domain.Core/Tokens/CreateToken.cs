using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Tokens;

/// <summary>
/// DTO for creating a new authorization token
/// </summary>
public class CreateToken
{
    /// <summary>
    /// Token identifier
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// The value that goes into the confirmation link, for the types that mail one.
    /// </summary>
    /// <remarks>
    /// Never stored: the row keeps <see cref="SecretHash" /> instead. Empty for an
    /// invitation, which is redeemed by the addressee inside the site and is
    /// therefore an identifier rather than a credential — see
    /// <see cref="ConfirmationSecret" />.
    /// </remarks>
    public Guid Secret { get; set; }

    /// <summary>
    /// What the row keeps for <see cref="Secret" />.
    /// </summary>
    public byte[]? SecretHash { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Related entity identifier (game, blog, etc.)
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Token type
    /// </summary>
    public TokenType Type { get; set; }
}
