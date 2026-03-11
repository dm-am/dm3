using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Exceptions;

namespace DM.Web.API.Features.Moderation.UsernameChanges;

/// <summary>
/// API service for username change request moderation
/// </summary>
internal class UsernameChangeApiService : IUsernameChangeApiService
{
    private readonly IUsernameChangeService _usernameChangeService;
    private readonly IMapper _mapper;

    public UsernameChangeApiService(
        IUsernameChangeService usernameChangeService,
        IMapper mapper)
    {
        _usernameChangeService = usernameChangeService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UsernameChangeRequest>> GetPendingRequestsAsync()
    {
        var requests = await _usernameChangeService.GetPendingRequestsAsync();
        return requests.Select(r => _mapper.Map<UsernameChangeRequest>(r));
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequest> GetByIdAsync(Guid id)
    {
        var request = await _usernameChangeService.GetByIdAsync(id);
        if (request == null)
            throw new HttpException(HttpStatusCode.NotFound, "Username change request not found");
        return _mapper.Map<UsernameChangeRequest>(request);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequest> ResolveAsync(Guid id, ResolveUsernameChangeRequest resolve)
    {
        var domainResolve = new DM.Domain.Account.Features.UsernameChange.ResolveUsernameChangeRequest
        {
            RequestId = id,
            Status = resolve.Status,
            Comment = resolve.Comment
        };
        var result = await _usernameChangeService.ResolveAsync(domainResolve);
        return _mapper.Map<UsernameChangeRequest>(result);
    }
}
