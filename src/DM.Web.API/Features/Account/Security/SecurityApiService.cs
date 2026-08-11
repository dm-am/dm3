using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Security;
using DomainSecurityEventType = DM.Domain.Account.Features.Security.SecurityEventType;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Account.Security;

/// <inheritdoc />
internal class SecurityApiService : ISecurityApiService
{
    private readonly ISecurityJournalService _journal;

    /// <summary>
    /// Creates a new instance of SecurityApiService
    /// </summary>
    public SecurityApiService(ISecurityJournalService journal)
    {
        _journal = journal;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SecurityEvent>> GetSecurityLogs(SecurityLogType? type = null, int limit = 50)
    {
        var events = await _journal.GetOwnAsync(type, limit);
        return events.Select(MapToDto).ToList();
    }

    private static SecurityEvent MapToDto(SecurityAuditEntry entry) => new()
    {
        Id = entry.Id,
        EventType = (SecurityEventType)entry.EventType,
        Description = GetEventDescription(entry.EventType),
        TimestampUtc = entry.TimestampUtc,
        IpAddress = entry.IpAddress,
        DeviceInfo = entry.DeviceInfo,
        Details = entry.Details
    };

    private static string GetEventDescription(DomainSecurityEventType eventType) => eventType switch
    {
        DomainSecurityEventType.LoginSuccess => "Успешный вход",
        DomainSecurityEventType.LoginFailure => "Неудачная попытка входа",
        DomainSecurityEventType.Logout => "Выход",
        DomainSecurityEventType.PasswordChange => "Изменение пароля",
        DomainSecurityEventType.EmailChange => "Изменение почты",
        DomainSecurityEventType.SessionTerminated => "Завершение сессии",
        DomainSecurityEventType.LogoutElsewhere => "Выход со всех устройств",
        DomainSecurityEventType.PasswordResetRequest => "Запрос сброса пароля",
        DomainSecurityEventType.PasswordResetComplete => "Пароль сброшен",
        DomainSecurityEventType.AccountLocked => "Аккаунт заблокирован",
        DomainSecurityEventType.SuspiciousLogin => "Подозрительный вход",
        _ => "Неизвестное событие"
    };
}
