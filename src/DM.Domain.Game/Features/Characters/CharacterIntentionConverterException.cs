using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Characters;

/// <summary>
/// Argument out of range exception for game status
/// </summary>
internal class CharacterIntentionConverterException : Exception
{
    /// <inheritdoc />
    public CharacterIntentionConverterException(CharacterStatus characterStatus)
        : base($"Character intention for status {characterStatus} is not defined")
    {
    }
}