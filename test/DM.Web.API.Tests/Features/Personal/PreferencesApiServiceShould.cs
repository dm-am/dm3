using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.Profiles;
using DM.Testing.Dsl;
using DM.Web.API.Features.Personal.Preferences;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

public class PreferencesApiServiceShould
{
    private const string Username = "Reader";

    private readonly IUserService _users = Substitute.For<IUserService>();
    private readonly PreferencesApiService _service;
    private UpdateUser? _written;

    public PreferencesApiServiceShould()
    {
        var identity = Substitute.For<IIdentityProvider>();
        identity.Current.Returns(Identities.User(Guid.NewGuid(), Username));
        _users.GetDetailsAsync(Username).Returns(Stored(pageSize: 20));
        _users.UpdateAsync(Arg.Do<UpdateUser>(update => _written = update))
            .Returns(_ => Stored(_written!.Settings!.PageSize));
        _service = new PreferencesApiService(_users, identity, new PreferencesMapper());
    }

    [Fact]
    public async Task LeaveTheRatingToTheProfile()
    {
        await _service.UpdateMyPreferences(new UpdatePreferencesRequest { PageSize = 50 });

        _written!.RatingDisabled.Should().BeNull("the participation in the rating is saved by the profile");
    }

    [Fact]
    public async Task WriteOnePageSizeAndKeepItWhenNotSent()
    {
        var answer = await _service.UpdateMyPreferences(new UpdatePreferencesRequest { PageSize = 50 });

        _written!.Settings!.PageSize.Should().Be(50);
        answer.PageSize.Should().Be(50);

        await _service.UpdateMyPreferences(new UpdatePreferencesRequest { TimeZoneId = "Europe/Moscow" });
        _written.Settings!.PageSize.Should().Be(20, "the stored size, as the request said nothing of it");
    }

    [Fact]
    public async Task ReadThePageSizeFromTheSettings()
    {
        _users.GetDetailsAsync(Username).Returns(Stored(pageSize: 30));

        var preferences = await _service.GetMyPreferences();

        preferences.PageSize.Should().Be(30);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(201)]
    public void RefuseAPageSizeTheFormDoesNotOffer(int pageSize) =>
        Refusals(new UpdatePreferencesRequest { PageSize = pageSize }).Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Недопустимое число на странице");

    [Fact]
    public void TakeEveryOfferedPageSize() =>
        PagingPolicy.AllowedPageSizes
            .SelectMany(size => Refusals(new UpdatePreferencesRequest { PageSize = size }))
            .Should().BeEmpty();

    private static UserDetails Stored(int pageSize) => new()
    {
        Username = Username,
        Settings = new UserSettings { PageSize = pageSize }
    };

    private static List<ValidationResult> Refusals(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }
}
