namespace DM.Infrastructure.Persistence.Entities.Contracts;

internal interface IRemovable
{
    bool IsRemoved { get; set; }
}
