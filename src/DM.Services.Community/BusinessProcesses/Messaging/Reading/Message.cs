using System;
using System.Collections.Generic;
using DM.Services.Common.Dto;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Messaging.Reading;

/// <summary>
/// Service DTO of message
/// </summary>
public class Message : ILikable
{
    /// <summary>
    /// Message identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Creating moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Message author
    /// </summary>
    public GeneralUser Author { get; set; }

    /// <summary>
    /// Message content
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Message is deleted (soft delete)
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Users who liked this message
    /// </summary>
    public IEnumerable<GeneralUser> Likes { get; set; }
}