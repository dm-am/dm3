using System.Linq;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// The unique indexes over lower(Email) and lower(Username) are what make a login lookup an
/// index scan instead of a sequential one, and what stops two accounts differing only by
/// letter case. EF cannot express an expression index, so they are asserted by the
/// application at startup rather than written into the migration by hand — which is what
/// keeps the migration safe to regenerate.
/// </summary>
public class ExpressionIndexShould : IntegrationTestBase
{
    public ExpressionIndexShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Theory]
    [InlineData("IX_Users_Email_Lower")]
    [InlineData("IX_Users_Username_Lower")]
    public async Task ExistAfterStartup(string indexName)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var definitions = await dbContext.Database
            .SqlQuery<string>($"""SELECT indexdef FROM pg_indexes WHERE indexname = {indexName}""")
            .ToListAsync();

        definitions.Should().ContainSingle(
            "the startup initializer asserts this index and re-asserts it on every start");
        definitions.Single().Should().Contain("lower").And.Contain("UNIQUE");
    }
}
