using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using FluentValidation;

namespace DM.Domain.Game.Features.GameAttributeSchemas;

/// <inheritdoc />
internal class GameAttributeSchemaService : IGameAttributeSchemaService
{
    private readonly IValidator<ReplaceGameAttributeSchema> _validator;
    private readonly IGameService _gameService;
    private readonly IIntentionManager _intentionManager;
    private readonly IGameAttributeSchemaRepository _repository;

    public GameAttributeSchemaService(
        IValidator<ReplaceGameAttributeSchema> validator,
        IGameService gameService,
        IIntentionManager intentionManager,
        IGameAttributeSchemaRepository repository)
    {
        _validator = validator;
        _gameService = gameService;
        _intentionManager = intentionManager;
        _repository = repository;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The order of the refusals is the order of the questions. A body that
    /// cannot be stored is refused before anything is read, so an unstorable
    /// request tells the caller nothing about which games exist. Then the game
    /// itself, resolved by the same read that serves its details, so a game this
    /// caller may not see answers the same 404 either way. Then the intention,
    /// so a visible game the caller does not lead answers 403. The version comes
    /// last, and inside the write: it is the only question the database can
    /// answer.
    ///
    /// The conflict below is that version and nothing else. A game holding no
    /// sheet row never arrives here: the repository fails on it, because a
    /// missing sheet is a defect of whoever wrote the game and reloading would
    /// hand the caller the same nothing again.
    /// </remarks>
    public async Task<GameAttributeSchema> ReplaceAsync(
        ReplaceGameAttributeSchema replace, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(replace, ct);

        var game = await _gameService.GetAsync(replace.GameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Edit, game);

        var replaced = await _repository.Replace(replace, ct);
        if (replaced == null)
        {
            throw new HttpException(HttpStatusCode.Conflict,
                "Схему только что изменили. Проверьте и сохраните снова");
        }

        return replaced;
    }
}
