using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.BusinessObjects.Users.Settings;
using DM.Services.DataAccess.MongoIntegration;
using DM.Services.DataAccess.RelationalStorage;

namespace DM.Services.Community.BusinessProcesses.Users.Updating;

/// <inheritdoc />
internal class UserUpdatingRepository : MongoCollectionRepository<UserSettings>, IUserUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly DmMongoClient _mongoClient;

    /// <inheritdoc />
    public UserUpdatingRepository(
        DmDbContext dbContext,
        DmMongoClient mongoClient) : base(mongoClient)
    {
        _dbContext = dbContext;
        _mongoClient = mongoClient;
    }

    /// <inheritdoc />
    public async Task UpdateUser(IUpdateBuilder<User> updateUser, IUpdateBuilder<UserSettings> settingsUpdate)
    {
        updateUser.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
        await settingsUpdate.UpdateFor(_mongoClient, true);
    }
}