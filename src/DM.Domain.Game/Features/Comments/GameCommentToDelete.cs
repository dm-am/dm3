using System;
using DM.Domain.Core.Comments;

namespace DM.Domain.Game.Features.Comments;

public class GameCommentToDelete : Comment
{
    /// <summary>
    /// The discussion key is the game itself
    /// </summary>
    public Guid GameId => EntityId;
}
