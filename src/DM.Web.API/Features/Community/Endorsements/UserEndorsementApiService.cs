using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Core.Users;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Endorsements;

/// <inheritdoc />
internal class UserEndorsementApiService : IUserEndorsementApiService
{
    private readonly IUserEndorsementService _endorsementService;
    private readonly IUserLookupService _userLookupService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserEndorsementApiService(
        IUserEndorsementService endorsementService,
        IUserLookupService userLookupService,
        IMapper mapper)
    {
        _endorsementService = endorsementService;
        _userLookupService = userLookupService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<UserEndorsement>> GetReceived(string username, UserEndorsementsQuery query)
    {
        var user = await _userLookupService.GetAsync(username);
        return await GetFiltered(query, new UserEndorsementFilter
        {
            RecipientId = user.UserId,
            Search = query.Search,
            SortBy = query.SortBy,
            SortOrder = query.SortOrder,
        });
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<UserEndorsement>> GetWritten(string username, UserEndorsementsQuery query)
    {
        var user = await _userLookupService.GetAsync(username);
        return await GetFiltered(query, new UserEndorsementFilter
        {
            AuthorId = user.UserId,
            Search = query.Search,
            SortBy = query.SortBy,
            SortOrder = query.SortOrder,
        });
    }

    private async Task<ListEnvelope<UserEndorsement>> GetFiltered(
        UserEndorsementsQuery query, UserEndorsementFilter filter)
    {
        var (endorsements, paging) = await _endorsementService.GetAllAsync(query, filter);
        var apiEndorsements = endorsements.Select(_mapper.Map<UserEndorsement>);
        return new ListEnvelope<UserEndorsement>(apiEndorsements, new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<UserEndorsement> Get(Guid id)
    {
        var endorsement = await _endorsementService.GetAsync(id);
        return _mapper.Map<UserEndorsement>(endorsement);
    }

    /// <inheritdoc />
    public async Task<UserEndorsement> Create(string username, CreateUserEndorsementRequest request)
    {
        var user = await _userLookupService.GetAsync(username);
        var createEndorsement = new CreateUserEndorsement
        {
            TargetUserId = user.UserId,
            Text = request.Text
        };
        var endorsement = await _endorsementService.CreateAsync(createEndorsement);
        return _mapper.Map<UserEndorsement>(endorsement);
    }

    /// <inheritdoc />
    public async Task<UserEndorsement> Update(Guid id, UpdateUserEndorsementRequest request)
    {
        var updateEndorsement = new UpdateUserEndorsement
        {
            EndorsementId = id,
            Text = request.Text
        };
        var endorsement = await _endorsementService.UpdateAsync(updateEndorsement);
        return _mapper.Map<UserEndorsement>(endorsement);
    }

    /// <inheritdoc />
    public Task Delete(Guid id) => _endorsementService.DeleteAsync(id);
}
