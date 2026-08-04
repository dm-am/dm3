using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;

using FluentValidation;

namespace DM.Domain.Game.Features.Games;

/// <inheritdoc />
internal class GameCreationValidator : IGameCreationValidator
{
    private readonly IValidator<CreateGame> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;

    public GameCreationValidator(
        IValidator<CreateGame> validator,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task ValidateAndAuthorize(CreateGame createGame)
    {
        await _validator.ValidateAndThrowAsync(createGame);
        _intentionManager.ThrowIfForbidden(GameIntention.Create);
    }

    public (ModuleStatus initialStatus, PremoderationStatus premoderationStatus, bool isRecruitmentOpen) GetInitialGameState(
        CreateGame createGame)
    {
        var identity = _identityProvider.Current;
        var requiresPremoderation = identity.User.IsNewbie;

        var initialStatus = createGame.Draft ? ModuleStatus.Draft : ModuleStatus.Active;
        var premoderationStatus = requiresPremoderation
            ? PremoderationStatus.AwaitingApproval
            : PremoderationStatus.Approved;

        // Recruitment is open if game is Active (not Draft) and not requiring premoderation
        var isRecruitmentOpen = initialStatus == ModuleStatus.Active && !requiresPremoderation;

        return (initialStatus, premoderationStatus, isRecruitmentOpen);
    }
}
