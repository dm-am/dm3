using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.BusinessProcesses.Tokens;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.MessageQueuing.GeneralBus;
using DbGame = DM.Services.DataAccess.BusinessObjects.Games.Game;

namespace DM.Services.Game.BusinessProcesses.Games.AssistantAssignment;

/// <inheritdoc />
internal class AssignmentService : IAssignmentService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ITokenFactory _tokenFactory;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IAssignmentRepository _repository;
    private readonly IInvokedEventProducer _producer;

    /// <inheritdoc />
    public AssignmentService(
        IIdentityProvider identityProvider,
        ITokenFactory tokenFactory,
        IUpdateBuilderFactory updateBuilderFactory,
        IAssignmentRepository repository,
        IInvokedEventProducer producer)
    {
        _identityProvider = identityProvider;
        _tokenFactory = tokenFactory;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task CreateAssignment(Guid gameId, Guid userId)
    {
        var updates = (await _repository.FindAssignments(gameId))
            .Select(id => _updateBuilderFactory.Create<Token>(id).Field(t => t.IsRemoved, true));
        var token = _tokenFactory.Create(userId, gameId, TokenType.AssistantAssignment);
        await _repository.InvalidateAndCreate(updates, token);
        await _producer.Send(EventType.AssignmentRequestCreated, token.TokenId);
    }

    /// <inheritdoc />
    public Task AcceptAssignment(Guid tokenId) => Assign(tokenId, true);

    /// <inheritdoc />
    public Task RejectAssignment(Guid tokenId) => Assign(tokenId, false);

    private async Task Assign(Guid tokenId, bool accept)
    {
        var userId = _identityProvider.Current.User.UserId;
        var gameId = await _repository.FindGameToAssign(tokenId, userId);
        if (!gameId.HasValue)
        {
            throw new HttpException(HttpStatusCode.Gone,
                "Activation token is invalid! Address the technical support for further assistance");
        }

        var updateToken = _updateBuilderFactory.Create<Token>(tokenId).Field(t => t.IsRemoved, true);
        var updateGame = _updateBuilderFactory.Create<DbGame>(gameId.Value);
        var eventType = EventType.AssignmentRequestRejected;
        if (accept)
        {
            updateGame = updateGame.Field(g => g.AssistantId, userId);
            eventType = EventType.AssignmentRequestAccepted;
        }

        await _repository.AssignAssistant(updateGame, updateToken);
        await _producer.Send(eventType, tokenId);
    }
}