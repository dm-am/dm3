using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Personal.Preferences;
using AwesomeAssertions;
using Xunit;
using ServiceUserSettings = DM.Domain.Core.Identity.UserSettings;

namespace DM.Web.API.Tests.Features.Personal;

// The preferences are the settings document, and a document that never had a page size reads as a
// reader who never chose one.
public class PreferencesMapperShould
{
    [Fact]
    public void ReadAZeroedPageSizeAsTheDefault()
    {
        var settings = new ServiceUserSettings { Theme = Theme.Dark, PageSize = 0 };

        var preferences = new PreferencesMapper().ToPreferences(settings);

        preferences.Theme.Should().Be(Theme.Dark);
        preferences.PageSize.Should().Be(PagingPolicy.DefaultPageSize,
            "a zeroed size is a reader with no preference, served what a new one is");
    }

    [Fact]
    public void WriteThePageSize()
    {
        var settings = new PreferencesMapper().ToUserSettings(
            new Preferences { Theme = Theme.Dark, PageSize = 50, TimeZoneId = "Europe/Moscow" });

        settings.PageSize.Should().Be(50);
        settings.Theme.Should().Be(Theme.Dark);
        settings.TimeZoneId.Should().Be("Europe/Moscow");
    }
}
