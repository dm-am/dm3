using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Features.Blacklists;

/// <summary>
/// Service for managing user blacklist
/// </summary>
public interface IUserBlacklistService
{
    /// <summary>
    /// Get current user's blacklist
    /// </summary>
    Task<IEnumerable<BlacklistEntry>> GetMyBlacklist(CancellationToken ct = default);

    Task<UserBlacklistSettings> GetSettings(CancellationToken ct = default);

    Task<UserBlacklistSettings> UpdateSettings(UserBlacklistSettings settings, CancellationToken ct = default);

    Task<BlacklistEntry> Block(OperateUserBlacklistLink dto, CancellationToken ct = default);

    Task Unblock(OperateUserBlacklistLink dto, CancellationToken ct = default);
}
