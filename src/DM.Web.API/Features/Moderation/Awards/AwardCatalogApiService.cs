using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
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
    private readonly AwardMapper _mapper;
    private readonly ModerationAwardMapper _moderationMapper;

    /// <inheritdoc />
    public AwardCatalogApiService(
        IAwardService awardService,
        AwardMapper mapper,
        ModerationAwardMapper moderationMapper)
    {
        _awardService = awardService;
        _mapper = mapper;
        _moderationMapper = moderationMapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<AwardType>> GetTypes()
    {
        var types = await _awardService.GetTypesAsync(includeInactive: true);
        return new ListEnvelope<AwardType>(types.Select(_mapper.ToAwardType));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ContestSeries>> GetSeries()
    {
        var series = await _awardService.GetSeriesAsync(includeInactive: true);
        return new ListEnvelope<ContestSeries>(series.Select(_mapper.ToContestSeries));
    }

    /// <inheritdoc />
    public async Task<Envelope<AwardType>> CreateType(CreateAwardTypeRequest request)
    {
        var domain = _mapper.ToCreateAwardType(request);
        var created = await _awardService.CreateTypeAsync(domain);
        return new Envelope<AwardType>(_mapper.ToAwardType(created));
    }

    /// <inheritdoc />
    public async Task<Envelope<AwardType>> UpdateType(Guid id, UpdateAwardTypeRequest request)
    {
        var domain = _mapper.ToUpdateAwardType(request);
        domain.Id = id;
        var updated = await _awardService.UpdateTypeAsync(domain);
        return new Envelope<AwardType>(_mapper.ToAwardType(updated));
    }

    /// <inheritdoc />
    public Task DeactivateType(Guid id) => _awardService.DeactivateTypeAsync(id);

    /// <inheritdoc />
    public async Task<ListEnvelope<ContestSeriesAward>> GetSeriesAwards(Guid seriesId)
    {
        var awards = await _awardService.GetSeriesAwardsAsync(seriesId);
        return new ListEnvelope<ContestSeriesAward>(awards.Select(_moderationMapper.ToContestSeriesAward));
    }

    /// <inheritdoc />
    public async Task<Envelope<ContestSeries>> CreateSeries(CreateContestSeriesRequest request)
    {
        var domain = _mapper.ToCreateContestSeries(request);
        var created = await _awardService.CreateSeriesAsync(domain);
        return new Envelope<ContestSeries>(_mapper.ToContestSeries(created));
    }

    /// <inheritdoc />
    public async Task<Envelope<ContestSeries>> UpdateSeries(Guid id, UpdateContestSeriesRequest request)
    {
        var domain = _mapper.ToUpdateContestSeries(request);
        domain.Id = id;
        var updated = await _awardService.UpdateSeriesAsync(domain);
        return new Envelope<ContestSeries>(_mapper.ToContestSeries(updated));
    }

    /// <inheritdoc />
    public Task DeactivateSeries(Guid id) => _awardService.DeactivateSeriesAsync(id);
}
