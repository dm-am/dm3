using System;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Game.BusinessProcesses.Games.Updating;

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