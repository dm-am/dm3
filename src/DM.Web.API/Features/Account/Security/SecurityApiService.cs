using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Account.Features.Security;
using DomainSecurityEventType = DM.Domain.Account.Features.Security.SecurityEventType;

namespace DM.Web.API.Features.Account.Security;

/// <inheritdoc />
internal class SecurityApiService : ISecurityApiService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityAuditService _securityAuditService;

    /// <summary>
    /// Creates a new instance of SecurityApiService
    /// </summary>
    public SecurityApiService(
        IIdentityProvider identityProvider,
        ISecurityAuditService securityAuditService)
    {
        _identityProvider = identityProvider;
        _securityAuditService = securityAuditService;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SecurityEvent>> GetSecurityLogs(string? type = null, int limit = 50)
    {
        var currentUserId = _identityProvider.Current.User.UserId;

        IEnumerable<SecurityAuditEntry> events = type?.ToLowerInvariant() switch
        {
            "login" => await _securityAuditService.GetLoginHistoryAsync(currentUserId, limit),
            "password" => await _securityAuditService.GetPasswordEventsAsync(currentUserId, limit),
            "session" => await _securityAuditService.GetSessionEventsAsync(currentUserId, limit),
            _ => await _securityAuditService.GetRecentEventsAsync(currentUserId, limit)
        };

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
        DomainSecurityEventType.EmailChange => "Изменение email",
        DomainSecurityEventType.SessionTerminated => "Завершение сессии",
        DomainSecurityEventType.LogoutElsewhere => "Выход со всех устройств",
        DomainSecurityEventType.PasswordResetRequest => "Запрос сброса пароля",
        DomainSecurityEventType.PasswordResetComplete => "Пароль сброшен",
        DomainSecurityEventType.AccountLocked => "Аккаунт заблокирован",
        DomainSecurityEventType.SuspiciousLogin => "Подозрительный вход",
        _ => "Неизвестное событие"
    };
}
