using System.Linq;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Identity;
using Riok.Mapperly.Abstractions;
using DbPendingRegistration = DM.Infrastructure.Persistence.Entities.Account.PendingRegistration;
using DbTwoFactorChallenge = DM.Infrastructure.Persistence.Entities.Account.TwoFactorChallenge;
using DbUserLoginRecord = DM.Infrastructure.Persistence.Entities.Account.UserLoginRecord;
using DbUserSession = DM.Infrastructure.Persistence.Entities.Account.UserSession;
using DbUserTwoFactor = DM.Infrastructure.Persistence.Entities.Account.UserTwoFactor;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <summary>
/// Compile-time mapper for account entities
/// </summary>
[Mapper]
internal static partial class AccountMapper
{
    /// <summary>
    /// Session row to the domain session. IsCurrent is not a column: whether
    /// the row is the caller's own session is known only to the caller.
    /// </summary>
    [MapProperty(nameof(DbUserSession.SessionId), nameof(Session.Id))]
    [MapperIgnoreTarget(nameof(Session.IsCurrent))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial Session ToSession(this DbUserSession session);

    /// <summary>
    /// Pending registration row to the domain record. The secret is not a column:
    /// the row holds only its hash.
    /// </summary>
    [MapperIgnoreTarget(nameof(PendingRegistration.Secret))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial PendingRegistration ToPendingRegistration(this DbPendingRegistration pending);

    /// <summary>
    /// EF projection to the state of the second factor. The member configuration lives on
    /// <see cref="ToTwoFactorState" />, which the projection inlines.
    /// </summary>
    public static partial IQueryable<TwoFactorState> ProjectToTwoFactorState(this IQueryable<DbUserTwoFactor> query);

    [MapperIgnoreTarget(nameof(TwoFactorState.IsConfirmed))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    internal static partial TwoFactorState ToTwoFactorState(this DbUserTwoFactor factor);

    /// <summary>EF projection to an unfinished sign-in, configured on <see cref="ToChallengeState" /></summary>
    public static partial IQueryable<TwoFactorChallengeState> ProjectToChallengeState(
        this IQueryable<DbTwoFactorChallenge> query);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    internal static partial TwoFactorChallengeState ToChallengeState(this DbTwoFactorChallenge challenge);

    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial DbTwoFactorChallenge ToRow(this TwoFactorChallengeState challenge);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial UserLoginRecord ToLoginRecord(this DbUserLoginRecord record);

    [MapperRequiredMapping(RequiredMappingStrategy.Source)]
    public static partial DbUserLoginRecord ToRow(this UserLoginRecord record);
}
