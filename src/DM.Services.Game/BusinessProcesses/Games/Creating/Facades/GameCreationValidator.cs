using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Configuration;
using DM.Services.Core.Dto.Enums;
using DM.Services.Game.Authorization;
using DM.Services.Game.Dto.Input;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DM.Services.Game.BusinessProcesses.Games.Creating.Facades;

/// <inheritdoc />
internal class GameCreationValidator : IGameCreationValidator
{
    private readonly IValidator<CreateGame> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ProbationConfiguration _probationConfig;

    public GameCreationValidator(
        IValidator<CreateGame> validator,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        IOptions<ProbationConfiguration> probationConfig)
    {
        _validator = validator;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _probationConfig = probationConfig.Value;
    }

    /// <inheritdoc />
    public async Task ValidateAndAuthorize(CreateGame createGame)
    {
        await _validator.ValidateAndThrowAsync(createGame);
        _intentionManager.ThrowIfForbidden(GameIntention.Create);
    }

    /// <inheritdoc />
    public (ModuleStatus initialStatus, PremoderationStatus premoderationStatus, bool isRecruitmentOpen) GetInitialGameState(
        CreateGame createGame)
    {
        var identity = _identityProvider.Current;
        var requiresPremoderation = identity.User.QuantityRating < _probationConfig.NewbiePostThreshold;
        var initialStatus = createGame.Draft ? ModuleStatus.Draft : ModuleStatus.Active;
        var premoderationStatus = requiresPremoderation
            ? PremoderationStatus.AwaitingApproval
            : PremoderationStatus.Approved;

        // Recruitment is open if game is Active (not Draft) and not requiring premoderation
        var isRecruitmentOpen = initialStatus == ModuleStatus.Active && !requiresPremoderation;

        return (initialStatus, premoderationStatus, isRecruitmentOpen);
    }
}
