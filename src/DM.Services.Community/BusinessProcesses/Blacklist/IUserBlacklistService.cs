using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Blacklist;

/// <summary>
/// Service for managing user blacklist
/// </summary>
public interface IUserBlacklistService
{
    /// <summary>
    /// Get current user's blacklist
    /// </summary>
    Task<IEnumerable<BlacklistEntryDto>> GetMyBlacklist(CancellationToken ct = default);

    /// <summary>
    /// Get blacklist settings
    /// </summary>
    Task<UserBlacklistSettings> GetSettings(CancellationToken ct = default);

    /// <summary>
    /// Update blacklist settings
    /// </summary>
    Task<UserBlacklistSettings> UpdateSettings(UserBlacklistSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Block a user
    /// </summary>
    Task<BlacklistEntryDto> BlockUser(string login, string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Unblock a user
    /// </summary>
    Task UnblockUser(string login, CancellationToken ct = default);

    /// <summary>
    /// Check if a user is blocked by current user
    /// </summary>
    Task<bool> IsBlocked(string login, CancellationToken ct = default);

    /// <summary>
    /// Check if current user can send message to another user
    /// </summary>
    Task<bool> CanSendMessage(Guid targetUserId, CancellationToken ct = default);
}

/// <summary>
/// Blacklist entry DTO
/// </summary>
public class BlacklistEntryDto
{
    /// <summary>Entry identifier</summary>
    public Guid Id { get; set; }

    /// <summary>Blocked user login</summary>
    public string Login { get; set; } = null!;

    /// <summary>Reason for blocking</summary>
    public string? Reason { get; set; }

    /// <summary>Creation date</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
