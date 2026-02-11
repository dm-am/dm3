using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.DataAccess.RelationalStorage;

namespace DM.Services.Game.BusinessProcesses.RoomAccesses.Deleting;

/// <inheritdoc />
internal class RoomAccessDeletingRepository : IRoomAccessDeletingRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public RoomAccessDeletingRepository(
        DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task Delete(IUpdateBuilder<RoomAccess> deleteLink)
    {
        deleteLink.AttachTo(_dbContext);
        return _dbContext.SaveChangesAsync();
    }
}