using DM.Domain.Core.Identity;
using Riok.Mapperly.Abstractions;
using DbUserSession = DM.Infrastructure.Persistence.Entities.Account.UserSession;

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
}
