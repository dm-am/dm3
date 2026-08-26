using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Warnings;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <inheritdoc />
internal class WarningApiService : IWarningApiService
{
    private readonly IWarningService _warningService;
    private readonly WarningMapper _mapper;

    /// <inheritdoc />
    public WarningApiService(IWarningService warningService, WarningMapper mapper)
    {
        _warningService = warningService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<UserWarningsInfo> GetUserWarnings(string login)
    {
        var warnings = await _warningService.GetUserWarnings(login);
        var points = await _warningService.GetUserWarningPoints(login);

        // Public endpoint: trim each warning to its aggregate facts
        var warningList = warnings.Select(_mapper.ToPublicWarning).ToList();

        return new UserWarningsInfo
        {
            Username = login,
            TotalPoints = points,
            ActiveCount = warningList.Count,
            Warnings = warningList
        };
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Warning>> GetAllWarnings(string? userLogin = null)
    {
        var warnings = await _warningService.GetAllWarnings(userLogin);
        return new ListEnvelope<Warning>(warnings.Select(_mapper.ToWarning));
    }

    /// <inheritdoc />
    public async Task<Envelope<Warning>> CreateWarning(CreateWarningRequest request)
    {
        var createWarning = new CreateWarning
        {
            Username = request.Username,
            EntityId = request.EntityId,
            EntityType = request.EntityType,
            Points = request.Points,
            Reason = request.Reason
        };

        var warning = await _warningService.CreateWarning(createWarning);
        return new Envelope<Warning>(_mapper.ToWarning(warning));
    }

    /// <inheritdoc />
    public Task RemoveWarning(Guid warningId)
    {
        return _warningService.RemoveWarning(warningId);
    }
}
