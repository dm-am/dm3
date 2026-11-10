using System.Net.Http.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Web.API.IntegrationTests.Helpers;

public abstract class ViewerRequestTestBase : IntegrationTestBase
{
    protected ViewerRequestTestBase(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // A request as the viewer, under an ordinary ban when asked; a guest sends none.
    protected Task<HttpResponseMessage> Send(
        GeneralUser? user, bool banned, HttpMethod method, string url, object? body = null)
    {
        var request = user is null
            ? new HttpRequestMessage(method, url)
            : CreateAuthenticatedRequest(method, url, user);
        if (user is not null && banned)
        {
            request.Headers.Add(TestAuthenticationMiddleware.TestUserAccessPolicyHeader,
                AccessPolicy.DemocraticBan.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return Client.SendAsync(request);
    }
}
