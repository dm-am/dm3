using System;
using System.Threading.Tasks;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Game.BusinessProcesses.Games.Creating.Facades;

/// <inheritdoc />
internal class GameInitializationService : IGameInitializationService
{
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IInvokedEventProducer _producer;

    public GameInitializationService(
        IUnreadCountersRepository countersRepository,
        IInvokedEventProducer producer)
    {
        _countersRepository = countersRepository;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task InitializeCounters(Guid gameId, Guid roomId)
    {
        await _countersRepository.Create(roomId, UnreadEntryType.Message);
        await _countersRepository.Create(gameId, UnreadEntryType.Message);
        await _countersRepository.Create(gameId, UnreadEntryType.Character);
    }

    /// <inheritdoc />
    public Task PublishGameCreated(Guid gameId)
    {
        return _producer.Send(EventType.NewGame, gameId);
    }
}
