using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace DM.Domain.Game.Features.Characters;
/// <summary>
/// Filler for character attribute values
/// </summary>
internal interface ICharacterAttributeValueFiller
{
    /// <summary>
    /// Fill character attribute values with needed metadata from the game
    /// attribute schema, redacting hidden attribute values for viewers who are
    /// neither the character owner nor a game lead (master/assistant).
    /// </summary>
    /// <param name="characters">Characters to fill</param>
    /// <param name="game">Game the characters belong to (schema id + roles)</param>
    /// <param name="viewerId">Current viewer identifier (Guid.Empty for guests)</param>
    Task Fill(IEnumerable<Character> characters, Games.Game game, Guid viewerId);
}
