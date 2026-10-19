using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Security;

/// <inheritdoc />
internal class SecurityJournalService : ISecurityJournalService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityAuditRepository _repository;

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
        var userId = _identityProvider.RequireAuthenticated().UserId;

        return SecurityEventCategories.For(type) is { } eventTypes
            ? await _repository.GetByTypesAsync(userId, eventTypes, take)
            : await _repository.GetRecentEventsAsync(userId, take);
    }
}
