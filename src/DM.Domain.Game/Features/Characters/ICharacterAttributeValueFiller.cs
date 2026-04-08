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
    /// Fill character attribute values with needed metadata from game attribute schema
    /// </summary>
    /// <param name="characters">Characters to fill</param>
    /// <param name="schemaId">Attribute schema identifier</param>
    Task Fill(IEnumerable<Character> characters, Guid? schemaId);
}
