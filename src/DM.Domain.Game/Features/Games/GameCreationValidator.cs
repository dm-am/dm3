using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Game.Authorization;

using FluentValidation;

namespace DM.Domain.Game.Features.Games;

/// <inheritdoc />
internal class GameCreationValidator : IGameCreationValidator
{
    private readonly IValidator<CreateGame> _validator;
    private readonly IIntentionManager _intentionManager;

    public GameCreationValidator(
        IValidator<CreateGame> validator,
        IIntentionManager intentionManager)
    {
        _validator = validator;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task ValidateAndAuthorize(CreateGame createGame)
    {
        await _validator.ValidateAndThrowAsync(createGame);
        _intentionManager.ThrowIfForbidden(GameIntention.Create);
    }
}
