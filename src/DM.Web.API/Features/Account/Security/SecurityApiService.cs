using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Account.Security;

public class SecurityApiService
{
    private readonly ISecurityJournalService _journal;

    public SecurityApiService(ISecurityJournalService journal)
    {
        _journal = journal;
    }

    public async Task<IEnumerable<SecurityEvent>> GetSecurityLogs(SecurityLogType? type, int take)
    {
        var events = await _journal.GetOwnAsync(type, take);
        return events.Select(MapToDto).ToList();
    }

    private static SecurityEvent MapToDto(SecurityAuditEntry entry) => new()
    {
        Id = entry.Id,
        EventType = entry.EventType,
        TimestampUtc = entry.TimestampUtc,
        IpAddress = entry.IpAddress,
        DeviceInfo = entry.DeviceInfo,
        Details = entry.Details
    };
}
