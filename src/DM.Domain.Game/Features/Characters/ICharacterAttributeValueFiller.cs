using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.Characters;

internal interface ICharacterAttributeValueFiller
{
    /// <summary>
    /// Fill attribute values with the schema metadata, redacting hidden values
    /// for anyone but the character owner and the leads; <paramref name="viewerId"/> is Guid.Empty for guests.
    /// </summary>
    Task Fill(IEnumerable<Character> characters, Games.Game game, Guid viewerId);
}
