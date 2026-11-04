using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.UserEndorsements;

public interface IUserEndorsementService
{
    Task<UserEndorsement> CreateAsync(CreateUserEndorsement createEndorsement);

    /// <summary>
    /// Tell whether the current user may write a recommendation about the
    /// given user, and why not when they may not
    /// </summary>
    /// <remarks>
    /// Answered by the same evaluation <see cref="CreateAsync"/> refuses by,
    /// so a client that hides the control on a refusal here hides exactly the
    /// control the create call would have rejected.
    /// </remarks>
    Task<UserEndorsementEligibility> GetEligibilityAsync(Guid targetUserId);

    /// <returns>Endorsement or throws if not found</returns>
    Task<UserEndorsement> GetAsync(Guid id);

    Task<(IEnumerable<UserEndorsement> Endorsements, PagingResult Paging)> GetAllAsync(PagingQuery query, UserEndorsementFilter? filter = null);

    Task<UserEndorsement> UpdateAsync(UpdateUserEndorsement updateEndorsement);

    Task DeleteAsync(Guid id);
}
