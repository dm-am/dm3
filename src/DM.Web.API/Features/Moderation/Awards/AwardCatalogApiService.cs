using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using DM.Web.API.Features.Community.Awards;
using DM.Web.API.Shared.Dto;
using IAwardService = DM.Domain.Community.Features.Awards.IAwardService;
using DomainCreateAwardType = DM.Domain.Community.Features.Awards.CreateAwardType;
using DomainUpdateAwardType = DM.Domain.Community.Features.Awards.UpdateAwardType;
using DomainCreateContestSeries = DM.Domain.Community.Features.Awards.CreateContestSeries;
using DomainUpdateContestSeries = DM.Domain.Community.Features.Awards.UpdateContestSeries;

namespace DM.Web.API.Features.Moderation.Awards;

/// <inheritdoc />
internal class AwardCatalogApiService : IAwardCatalogApiService
{
    private readonly IAwardService _awardService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AwardCatalogApiService(IAwardService awardService, IMapper mapper)
    {
        _awardService = awardService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<AwardType>> CreateType(CreateAwardTypeRequest request)
    {
        var domain = _mapper.Map<DomainCreateAwardType>(request);
        var created = await _awardService.CreateTypeAsync(domain);
        return new Envelope<AwardType>(_mapper.Map<AwardType>(created));
    }

    /// <inheritdoc />
    public async Task<Envelope<AwardType>> UpdateType(Guid id, UpdateAwardTypeRequest request)
    {
        var domain = _mapper.Map<DomainUpdateAwardType>(request);
        domain.Id = id;
        var updated = await _awardService.UpdateTypeAsync(domain);
        return new Envelope<AwardType>(_mapper.Map<AwardType>(updated));
    }

    /// <inheritdoc />
    public Task DeactivateType(Guid id) => _awardService.DeactivateTypeAsync(id);

    /// <inheritdoc />
    public async Task<ListEnvelope<ContestSeriesAward>> GetSeriesAwards(Guid seriesId)
    {
        var awards = await _awardService.GetSeriesAwardsAsync(seriesId);
        return new ListEnvelope<ContestSeriesAward>(_mapper.Map<IEnumerable<ContestSeriesAward>>(awards));
    }

    /// <inheritdoc />
    public async Task<Envelope<ContestSeries>> CreateSeries(CreateContestSeriesRequest request)
    {
        var domain = _mapper.Map<DomainCreateContestSeries>(request);
        var created = await _awardService.CreateSeriesAsync(domain);
        return new Envelope<ContestSeries>(_mapper.Map<ContestSeries>(created));
    }

    /// <inheritdoc />
    public async Task<Envelope<ContestSeries>> UpdateSeries(Guid id, UpdateContestSeriesRequest request)
    {
        var domain = _mapper.Map<DomainUpdateContestSeries>(request);
        domain.Id = id;
        var updated = await _awardService.UpdateSeriesAsync(domain);
        return new Envelope<ContestSeries>(_mapper.Map<ContestSeries>(updated));
    }

    /// <inheritdoc />
    public Task DeactivateSeries(Guid id) => _awardService.DeactivateSeriesAsync(id);
}
