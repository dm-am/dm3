using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Messages;

/// <summary>
/// Service DTO for message edit record
/// </summary>
public class MessageEdit
{
    /// <summary>
    /// Edit record identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Edit timestamp (UTC)
    /// </summary>
    public DateTimeOffset EditedAtUtc { get; set; }

    /// <summary>
    /// Editor user
    /// </summary>
    public GeneralUser Editor { get; set; } = null!;
}
