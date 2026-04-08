using AutoMapper;
using DM.Infrastructure.Persistence.Shared.Comments;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Shared.Comments;

public class CommentMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<GeneralUserMappingProfile>();
            cfg.AddProfile<CommentMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
