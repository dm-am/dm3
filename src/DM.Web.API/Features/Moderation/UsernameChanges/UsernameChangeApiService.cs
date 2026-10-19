using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Exceptions;
using DomainResolveUsernameChangeRequest = DM.Domain.Account.Features.UsernameChange.ResolveUsernameChangeRequest;

namespace DM.Web.API.Features.Moderation.UsernameChanges;

/// <summary>
/// API service for username change request moderation
/// </summary>
public class UsernameChangeApiService
{
    private readonly IUsernameChangeService _usernameChangeService;
    private readonly UsernameChangeMapper _mapper;

    public UsernameChangeApiService(
        IUsernameChangeService usernameChangeService,
        UsernameChangeMapper mapper)
    {
        _usernameChangeService = usernameChangeService;
        _mapper = mapper;
    }

    public async Task<IEnumerable<UsernameChangeRequest>> GetPendingRequestsAsync()
    {
        var requests = await _usernameChangeService.GetPendingRequestsAsync();
        return requests.Select(_mapper.ToRequest);
    }

    public async Task<UsernameChangeRequest> GetByIdAsync(Guid id)
    {
        var request = await _usernameChangeService.GetByIdAsync(id);
        if (request == null)
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UsernameChangeRequestNotFound);
        return _mapper.ToRequest(request);
    }

    public async Task<UsernameChangeRequest> ResolveAsync(Guid id, ResolveUsernameChangeRequest resolve)
    {
        var domainResolve = new DomainResolveUsernameChangeRequest
        {
            RequestId = id,
            Status = resolve.Status,
            Comment = resolve.Comment
        };
        var result = await _usernameChangeService.ResolveAsync(domainResolve);
        return _mapper.ToRequest(result);
    }
}
