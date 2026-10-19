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
    /// An unstorable body is refused before the game is read, so it tells nothing about which games exist.
    /// The conflict is the lost version and nothing else: a game without a sheet row fails in the repository.
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
                "Пока вы правили, схему сохранил кто-то другой. Ваши правки на месте, \"Сохранить\" запишет их поверх");
        }

        return replaced;
    }
}
