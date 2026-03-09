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

    /// <summary>
    /// User who created this token (for invitations)
    /// </summary>
    public Guid? CreatorId { get; set; }
}
