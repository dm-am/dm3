using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using DM.Web.API.Features.Account.Authentication;
using DM.Web.API.Features.Account.Registration;
using DM.Web.API.Features.General.Tickets;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.Account;

/// <summary>
/// A honeypot refusal is filed under a field the request declares.
/// </summary>
/// <remarks>
/// Three intake forms carry the same hidden field and refuse the same way. The
/// login one filed its sentence under "identifier" - a name no request of this API
/// declares and no form reads - so the one answer a caught client gets was
/// addressed to nothing, while the two forms beside it named a real field.
/// Asserted against the properties of the request type rather than against the
/// literal key: a key spelled right today and left behind by a renamed field
/// tomorrow is this same defect again.
/// </remarks>
public class HoneypotRefusalShould : UnitTestBase
{
    private const string Filled = "http://spam.example";

    [Fact]
    public async Task NameAFieldOfTheLoginRequest()
    {
        var api = Mock<IAuthenticationApiService>();
        var controller = new AuthenticationController(api.Object);

        var refusal = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => controller.Login(new LoginRequest
            {
                Email = "bot@example.com",
                Password = "whatever",
                Website = Filled
            }));

        ShouldNameAVisibleFieldOf<LoginRequest>(refusal);
        api.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NameAFieldOfTheRegistrationRequest()
    {
        var registration = Mock<IRegistrationApiService>();
        var activation = Mock<IActivationApiService>();
        var controller = new RegistrationController(registration.Object, activation.Object);

        var refusal = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => controller.Register(new RegistrationRequest
            {
                Email = "bot@example.com",
                Password = "whatever",
                AcceptedRules = true,
                Website = Filled
            }));

        ShouldNameAVisibleFieldOf<RegistrationRequest>(refusal);
        registration.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NameAFieldOfTheTicketRequest()
    {
        var tickets = Mock<ITicketIntakeApiService>();
        var controller = new TicketIntakeController(tickets.Object);

        var refusal = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => controller.CreateTicketIntake(new CreateTicketIntakeRequest
            {
                Subject = "anything",
                Text = "anything",
                Website = Filled
            }));

        ShouldNameAVisibleFieldOf<CreateTicketIntakeRequest>(refusal);
        tickets.VerifyNoOtherCalls();
    }

    private static void ShouldNameAVisibleFieldOf<TRequest>(HttpBadRequestException refusal)
    {
        var field = refusal.ValidationErrors.Keys.Single().ToLowerInvariant();

        Fields<TRequest>().Should().Contain(field,
            "a client files the sentence under the field the answer names, and a name " +
            "the request does not carry belongs to no field on the form");
        field.Should().NotBe("website",
            "the honeypot is the one field the form hides, so nothing filed under it " +
            "is shown either");
    }

    private static IEnumerable<string> Fields<TRequest>() => typeof(TRequest)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => property.Name.ToLowerInvariant());
}
