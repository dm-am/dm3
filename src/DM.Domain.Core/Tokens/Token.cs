using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Tokens;

/// <summary>
/// DTO for token (read)
/// </summary>
public class Token
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
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Token type
    /// </summary>
    public TokenType Type { get; set; }

    /// <summary>
    /// Whether token is removed
    /// </summary>
    public bool IsRemoved { get; set; }
}
