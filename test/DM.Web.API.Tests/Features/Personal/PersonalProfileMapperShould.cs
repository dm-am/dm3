using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Personal.Profiles;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

/// <summary>
/// PATCH carries "not sent" as null and the write model must keep saying it.
/// The contact list is the loudest case: null leaves the contacts alone, an
/// empty list removes them all.
/// </summary>
public class PersonalProfileMapperShould : UnitTestBase
{
    private PersonalProfileMapper CreateMapper() =>
        new(new UserMapper(Mock<IImgproxyUrlBuilder>()));

    [Fact]
    public void KeepAnUnsentContactListNull()
    {
        var update = CreateMapper().ToUpdateUser(new UpdateProfile
        {
            Status = "Новый статус"
        });

        update.Status.Should().Be("Новый статус");
        update.Contacts.Should().BeNull("null means \"leave the contacts alone\"");
        update.ShowBirthday.Should().BeNull();
        update.RatingDisabled.Should().BeNull();
        update.Name.Should().BeNull();
        update.Location.Should().BeNull();
        update.Info.Should().BeNull();
        update.AvatarUploadId.Should().BeNull();
    }

    [Fact]
    public void CarryAnEmptyContactListAsAClear()
    {
        var update = CreateMapper().ToUpdateUser(new UpdateProfile
        {
            Contacts = []
        });

        update.Contacts.Should().NotBeNull().And.BeEmpty(
            "an empty list is an instruction to remove all contacts");
    }

    [Fact]
    public void NumberContactsInSubmittedOrder()
    {
        var update = CreateMapper().ToUpdateUser(new UpdateProfile
        {
            Contacts =
            [
                new Contact { ContactType = "telegram", Value = "@one" },
                new Contact { ContactType = "discord", Value = "two" }
            ]
        });

        var contacts = update.Contacts.Should().HaveCount(2).And.Subject;
        contacts.Should().SatisfyRespectively(
            first =>
            {
                first.ContactType.Should().Be("telegram");
                first.ContactValue.Should().Be("@one");
                first.SortOrder.Should().Be(0);
            },
            second =>
            {
                second.ContactType.Should().Be("discord");
                second.ContactValue.Should().Be("two");
                second.SortOrder.Should().Be(1);
            });
    }

    /// <summary>
    /// A visibility block whose flag was not sent folds to null - the lifted
    /// negation must not invent a setting.
    /// </summary>
    [Fact]
    public void FoldAPartialVisibilityBlockWithoutInventingValues()
    {
        var update = CreateMapper().ToUpdateUser(new UpdateProfile
        {
            Visibility = new UpdateVisibilitySettings { ShowBirthday = false }
        });

        update.ShowBirthday.Should().BeFalse();
        update.RatingDisabled.Should().BeNull("showRating was not sent");
    }
}
