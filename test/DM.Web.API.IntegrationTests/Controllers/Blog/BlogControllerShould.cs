using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Blog;

public class BlogControllerShould : IntegrationTestBase
{
    public BlogControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Theory]
    [InlineData("/v1/blogs")]
    [InlineData("/v1/blogs?sortBy=popularity&take=10")]
    [InlineData("/v1/blogs?sortBy=popularity")]
    [InlineData("/v1/blogs?search=test")]
    [InlineData("/v1/blogs?search=test&sortBy=created")]
    [InlineData("/v1/blogs?search=test&sortBy=activated")]
    [InlineData("/v1/blogs?search=test&size=5&number=1")]
    public async Task ListTheBlogsForEveryQuery(string address)
    {
        var response = await Client.GetAsync(address);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);
        content.Should().Contain("resources");
    }

    [Fact]
    public async Task GetUserBlogsWithValidUserReturnsOk()
    {
        var response = await Client.GetAsync($"/v1/blogs/owner/{TestConstants.TestUserLogin}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
