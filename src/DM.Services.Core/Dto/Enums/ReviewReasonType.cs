using System;

namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Post review reason type (formerly VoteType)
/// </summary>
[Flags]
public enum ReviewReasonType
{
    /// <summary>
    /// No particular reason
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The post was fun to read
    /// </summary>
    Fun = 1 << 0,

    /// <summary>
    /// The post displays its author's roleplay skills
    /// </summary>
    Roleplay = 1 << 1,

    /// <summary>
    /// The post displays its author's writing skills
    /// </summary>
    Literature = 1 << 2
}
