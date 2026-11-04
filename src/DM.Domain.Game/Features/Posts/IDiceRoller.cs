using System;
using System.Collections.Generic;

namespace DM.Domain.Game.Features.Posts;

public interface IDiceRoller
{
    IReadOnlyList<DiceRoll> Roll(Guid postId, DateTimeOffset createdUtc, IEnumerable<CreatePostDiceRoll> specs);
}
