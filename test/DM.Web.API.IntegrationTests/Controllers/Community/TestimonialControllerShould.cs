using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

/// <summary>
/// Integration tests for Testimonial (website reviews) controller
/// </summary>
public class TestimonialControllerShould : IntegrationTestBase
{
    public TestimonialControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetTestimonials_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/testimonials");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTestimonials_ReturnsPaginatedResults()
    {
        // Act
        var response = await Client.GetAsync("/v1/testimonials?number=1&size=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostTestimonial_RequiresAuthentication()
    {
        // Arrange
        var testimonialData = new
        {
            text = "Great website for roleplay!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/testimonials", testimonialData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteTestimonial_RequiresAuthentication()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/testimonials/{TestConstants.TestTestimonialId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
