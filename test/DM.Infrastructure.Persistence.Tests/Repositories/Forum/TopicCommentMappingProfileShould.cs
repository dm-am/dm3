using AutoMapper;
using DM.Infrastructure.Persistence.Repositories.Forum;
using DM.Infrastructure.Persistence.Shared.Comments;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Forum;

public class TopicCommentMappingProfileShould : UnitTestBase
{
    [Fact]
    public void HaveValidConfiguration()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<CommentMappingProfile>();
            cfg.AddProfile<TopicCommentMappingProfile>();
            cfg.AddProfile<GeneralUserMappingProfile>();
        });
        configuration.AssertConfigurationIsValid();
    }
}
