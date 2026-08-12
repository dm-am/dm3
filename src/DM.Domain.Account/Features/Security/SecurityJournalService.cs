using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Security;

/// <inheritdoc />
internal class SecurityJournalService : ISecurityJournalService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityAuditRepository _repository;

    /// <inheritdoc />
    public SecurityJournalService(
        IIdentityProvider identityProvider,
        ISecurityAuditRepository repository)
    {
        _identityProvider = identityProvider;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetOwnAsync(
        SecurityLogType? type = null, int take = 50)
    {
        var identity = _identityProvider.Current;
        if (!identity.User.IsAuthenticated)
        {
            throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);
        }

        var userId = identity.User.UserId;

        // The last arm is "no filter asked for" and nothing else: a value outside
        // the vocabulary is refused by model binding, where the string form used
        // to land here and answer with the whole journal.
        return type switch
        {
            SecurityLogType.Login => await _repository.GetByTypesAsync(userId, SecurityEventCategories.Login, take),
            SecurityLogType.Password => await _repository.GetByTypesAsync(userId, SecurityEventCategories.Password, take),
            SecurityLogType.Session => await _repository.GetByTypesAsync(userId, SecurityEventCategories.Session, take),
            _ => await _repository.GetRecentEventsAsync(userId, take)
        };
    }
}
