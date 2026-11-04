using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// An example collection, announced by the administrator: the schema starts with nothing
    /// announced, as the live site does, and the bar of "Поддержка проекта" and /donate shows this one.
    /// </summary>
    /// <remarks>Draws nothing, so it moves no row of the fixture.</remarks>
    private async Task SeedFundraising(DbUser admin, ComprehensiveSeedResult result)
    {
        await _dbContext.FundraisingGoals.ExecuteUpdateAsync(goal => goal
            .SetProperty(g => g.Title, "Сервер и домен на год")
            .SetProperty(g => g.GoalAmount, 115000m)
            .SetProperty(g => g.CollectedAmount, 54923m)
            .SetProperty(g => g.ModifiedUtc, _now)
            .SetProperty(g => g.UpdatedByUserId, admin.UserId));

        result.Details.Add("Announced the example collection: 54923 of 115000");
    }
}
