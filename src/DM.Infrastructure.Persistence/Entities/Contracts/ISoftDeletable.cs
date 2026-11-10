using System;

namespace DM.Infrastructure.Persistence.Entities.Contracts;

internal interface ISoftDeletable : IRemovable
{
    Guid? DeletedByUserId { get; set; }

    DateTimeOffset? DeletedUtc { get; set; }
}
