using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Blacklists;

public interface IBlogBlacklistService
{
    Task<IEnumerable<GeneralUser>> Get(Guid blogId, CancellationToken ct = default);

    /// <returns>Blacklisted user</returns>
    Task<GeneralUser> Add(OperateBlogBlacklistLink dto, CancellationToken ct = default);

    Task Remove(OperateBlogBlacklistLink dto, CancellationToken ct = default);
}
