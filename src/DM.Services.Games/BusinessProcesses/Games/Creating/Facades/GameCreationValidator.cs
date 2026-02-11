using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.Dto.Input;
using FluentValidation;

namespace DM.Services.Gaming.BusinessProcesses.Games.Creating.Facades;

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

    /// <inheritdoc />
    public (GameStatus initialStatus, PremoderationStatus premoderationStatus, bool isRecruitmentOpen) GetInitialGameState(
        CreateGame createGame)
    {
        var identity = _identityProvider.Current;
        var requiresPremoderation = identity.User.QuantityRating < 100;
        var initialStatus = createGame.Draft ? GameStatus.Draft : GameStatus.Active;
        var premoderationStatus = requiresPremoderation
            ? PremoderationStatus.AwaitingApproval
            : PremoderationStatus.Approved;

        // Recruitment is open if game is Active (not Draft) and not requiring premoderation
        var isRecruitmentOpen = initialStatus == GameStatus.Active && !requiresPremoderation;

        return (initialStatus, premoderationStatus, isRecruitmentOpen);
    }
}
