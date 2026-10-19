using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.UserEndorsements;

public interface IUserEndorsementRepository
{
    Task<UserEndorsement?> GetAsync(Guid id);

    Task<int> CountAllAsync(UserEndorsementFilter? filter = null);

    Task<IEnumerable<UserEndorsement>> GetAllAsync(PagingData paging, UserEndorsementFilter? filter = null);

    Task<bool> ExistsAsync(Guid authorId, Guid targetUserId);

    Task<UserEndorsement> CreateAsync(CreateUserEndorsementEntity entity);

    Task<UserEndorsement> UpdateAsync(UpdateUserEndorsementEntity entity);

    Task<bool> HavePlayedTogetherAsync(Guid userId1, Guid userId2);

    /// <summary>
    /// Get user's total post count in games
    /// </summary>
    Task<int> GetUserPostCountAsync(Guid userId);
}
