using System;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Games.Posts;

namespace DM.Services.Gaming.BusinessProcesses.Games.Creating
{
    /// <inheritdoc />
    public class RoomFactory : IRoomFactory
    {
        private readonly IGuidFactory guidFactory;
        private const string DefaultRoomTitle = "Основная комната";

        /// <inheritdoc />
        public RoomFactory(
            IGuidFactory guidFactory)
        {
            this.guidFactory = guidFactory;
        }

        /// <inheritdoc />
        public Room Create(Guid gameId)
        {
            return new Room
            {
                RoomId = guidFactory.Create(),
                GameId = gameId,
                AccessType = RoomAccessType.Open,
                Type = RoomType.Default,
                Title = DefaultRoomTitle
            };
        }
    }
}