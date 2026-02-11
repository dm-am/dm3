using System;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.Game.Dto.Input;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Creating;

/// <summary>
/// Factory for room access DAL model
/// </summary>
internal interface IRoomAccessFactory
{
    /// <summary>
    /// Create DAL model
    /// </summary>
    /// <param name="roomAccess">DTO model</param>
    /// <param name="participantId">Participant identifier</param>
    /// <returns></returns>
    RoomAccess Create(CreateRoomAccess roomAccess, Guid participantId);
}