using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Argument out of range exception for game status
/// </summary>
internal class GameIntentionConverterException : Exception
{
    /// <inheritdoc />
    public GameIntentionConverterException(ModuleStatus moduleStatus)
        : base($"Game intention for status {moduleStatus} is not defined")
    {
    }
}