using System.Threading.Tasks;
using DM.Domain.Account.Features.Deactivation;

namespace DM.Web.API.Features.Account.Deactivation;

/// <inheritdoc />
internal class DeactivationApiService : IDeactivationApiService
{
    private readonly IDeactivationService _deactivationService;

    public DeactivationApiService(IDeactivationService deactivationService)
    {
        _deactivationService = deactivationService;
    }

    /// <inheritdoc />
    public Task Deactivate(DeactivationRequest request)
    {
        return _deactivationService.Deactivate(request.Password);
    }
}
