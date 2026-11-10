using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Features.Account.Authentication;
using DM.Web.API.Features.Account.Registration;
using DM.Web.API.Features.General.Tickets;
using DM.Web.API.Shared.BotTrap;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace DM.Web.API.Tests.Features.Account;

// Asserted against the properties of the request type, so a renamed field takes the key with it.
public class HoneypotRefusalShould
{
    private const string Filled = "http://spam.example";

    public static TheoryData<IHoneypotForm> FilledForms => new()
    {
        new LoginRequest { Email = "bot@example.com", Password = "whatever", Website = Filled },
        new RegistrationRequest
        {
            Email = "bot@example.com", Password = "whatever", AcceptedRules = true, Website = Filled
        },
        new CreateTicketIntakeRequest { Subject = "anything", Text = "anything", Website = Filled }
    };

    [Theory]
    [MemberData(nameof(FilledForms))]
    public void RefuseAFilledTrapUnderAVisibleField(IHoneypotForm form)
    {
        var refusal = Assert.Throws<HttpBadRequestException>(() => Filter(form));

        refusal.Message.Should().Be(RefusalMessage.InvalidData);
        var field = refusal.ValidationErrors.Keys.Single().ToLowerInvariant();
        Fields(form.GetType()).Should().Contain(field,
            "a client files the sentence under the field the answer names, and a name " +
            "the request does not carry belongs to no field on the form");
        field.Should().NotBe("website",
            "the honeypot is the one field the form hides, so nothing filed under it " +
            "is shown either");
    }

    [Fact]
    public void LetAnEmptyTrapThrough()
    {
        var act = () => Filter(new LoginRequest { Email = "reader@example.com", Password = "secret" });

        act.Should().NotThrow();
    }

    // The filter only sees a form that says it has a trap: a request with the field and without the
    // interface would go through with the field filled.
    [Fact]
    public void KnowEveryRequestThatCarriesATrap()
    {
        var untrapped = typeof(HoneypotFilter).Assembly.GetTypes()
            .Where(type => type.GetProperty(nameof(IHoneypotForm.Website)) != null)
            .Where(type => !typeof(IHoneypotForm).IsAssignableFrom(type))
            .Select(type => type.FullName);

        untrapped.Should().BeEmpty();
    }

    private static void Filter(object request)
    {
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(action, new List<IFilterMetadata>(),
            new Dictionary<string, object?> { ["request"] = request }, controller: new object());

        new HoneypotFilter().OnActionExecuting(context);
    }

    private static IEnumerable<string> Fields(Type request) => request
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => property.Name.ToLowerInvariant());
}
