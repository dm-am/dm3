using AutoMapper;
using DM.Testing;
using DM.Workers.SearchIndexer;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

public class SearchMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        // SearchMappingProfile is internal to the SearchIndexer worker
        // assembly (no dedicated test project exists), so it is picked up
        // through assembly scanning instead of a direct AddProfile call.
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(typeof(Startup).Assembly);
        });
        configuration.AssertConfigurationIsValid();
    }
}
