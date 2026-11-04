using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;
using DbSecurityAuditEntry = DM.Infrastructure.Persistence.Entities.Account.SecurityAuditEntry;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using SecurityEventType = DM.Domain.Account.Features.Security.SecurityEventType;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>The account whose second factor is on.</summary>
    internal const string TwoFactorLogin = "Player.Three";

    /// <summary>
    /// Its shared secret in Base32, as an authenticator takes it: known, so a code can be computed
    /// for the stand (TotpCalculator: HMAC-SHA1, thirty second steps, six digits). The store keeps
    /// it encrypted with the stand's key, as the setup does.
    /// </summary>
    internal const string TwoFactorSecret = "JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP";

    /// <summary>The account whose second factor an administrator took off.</summary>
    private const string FactorRemovedLogin = "TestUser";

    /// <summary>
    /// A second factor that is on, and one an administrator took off: the journals of the owner and
    /// of the administrator hold what TwoFactorRemovalService.ClearForColleague writes into each.
    /// </summary>
    /// <remarks>
    /// No recovery codes: their hash is the domain's own (RecoveryCodeFactory), so the account section
    /// says the codes have nearly run out and offers a new set.
    /// </remarks>
    private async Task SeedSecondFactor(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var owner = users.FirstOrDefault(u => u.Username == TwoFactorLogin);
        var removedFrom = users.FirstOrDefault(u => u.Username == FactorRemovedLogin);
        var admin = users.FirstOrDefault(u => u.Username == PrimaryLogin);
        if (owner == null || removedFrom == null || admin == null ||
            await _dbContext.UserTwoFactors.AnyAsync(f => f.UserId == owner.UserId))
        {
            result.Details.Add("Second factor skipped: its accounts are missing, or it is on already");
            return;
        }

        var enabledUtc = _now.AddDays(-20);
        _dbContext.UserTwoFactors.Add(new UserTwoFactor
        {
            UserId = owner.UserId,
            Secret = await _cryptoService.Encrypt(TwoFactorSecret),
            CreatedUtc = enabledUtc.AddMinutes(-2),
            ConfirmedUtc = enabledUtc,
            LastVerifiedUtc = enabledUtc,
        });
        AddJournalEntry(owner, SecurityEventType.TwoFactorEnabled, enabledUtc, null);

        var removedUtc = _now.AddDays(-10);
        AddJournalEntry(removedFrom, SecurityEventType.TwoFactorEnabled, _now.AddDays(-40), null);
        AddJournalEntry(removedFrom, SecurityEventType.TwoFactorRemovedByAdmin, removedUtc,
            RemovedByAdminOwnerDetails(admin.Username));
        AddJournalEntry(admin, SecurityEventType.TwoFactorRemovedByAdmin, removedUtc,
            RemovedByAdminDetails(removedFrom.Username));

        await _dbContext.SaveChangesAsync();
        result.Details.Add(
            $"Second factor on for {owner.Username}; taken off {removedFrom.Username} by {admin.Username}");
    }

    /// <summary>
    /// The owner's line of an administrator's removal, word for word as the service writes it.
    /// </summary>
    internal static string RemovedByAdminOwnerDetails(string adminUsername) =>
        $"Администратор: {adminUsername}";

    /// <summary>
    /// The administrator's line of the same removal, word for word as the service writes it.
    /// </summary>
    internal static string RemovedByAdminDetails(string username) =>
        $"Учетная запись: {username}, роль администратора: Администратор";

    private void AddJournalEntry(DbUser user, SecurityEventType type, DateTimeOffset at, string? details) =>
        _dbContext.SecurityAuditEntries.Add(new DbSecurityAuditEntry
        {
            SecurityAuditEntryId = _guidFactory.Create(),
            UserId = user.UserId,
            EventType = type,
            TimestampUtc = at.UtcDateTime,
            Details = details
        });
}
