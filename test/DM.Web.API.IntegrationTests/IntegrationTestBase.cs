using DM.Services.Core.Dto;
using Xunit;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Base class for integration tests.
/// Provides access to the shared database fixture and a configured HttpClient.
/// Uses a shared WebApplicationFactory to avoid resource exhaustion.
/// </summary>
[Collection("Database")]
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly DatabaseFixture DatabaseFixture;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(DatabaseFixture databaseFixture)
    {
        DatabaseFixture = databaseFixture;
        // Use shared factory from fixture to avoid creating multiple hosts
        Client = databaseFixture.Factory.CreateClient();
    }

    /// <summary>
    /// Create a new factory with an authenticated user.
    /// Note: This creates a separate factory that must be disposed.
    /// </summary>
    protected CustomWebApplicationFactory CreateAuthenticatedFactory(GeneralUser user)
    {
        return new CustomWebApplicationFactory(DatabaseFixture)
        {
            TestUser = user
        };
    }

    public void Dispose()
    {
        // Only dispose the client, not the shared factory
        Client.Dispose();
    }
}
