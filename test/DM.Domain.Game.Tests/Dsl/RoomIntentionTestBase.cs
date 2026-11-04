using System;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Tests.Dsl;

/// <summary>
/// The room resolver and the people of a game its cases ask about: the master,
/// an assistant, a player and a stranger to the game.
/// </summary>
public abstract class RoomIntentionTestBase
{
    private protected readonly RoomIntentionResolver resolver = new();

    protected static readonly Guid MasterId = Guid.NewGuid();
    protected static readonly Guid AssistantId = Guid.NewGuid();
    protected static readonly Guid PlayerId = Guid.NewGuid();
    protected static readonly Guid StrangerId = Guid.NewGuid();

    protected static GameDetails GameLedBy(Guid masterId, Guid? assistantId = null)
    {
        var builder = new GameBuilder().WithMaster(masterId);
        return assistantId.HasValue
            ? builder.WithAssistants(assistantId.Value).Please()
            : builder.Please();
    }
}
