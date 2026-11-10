using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Personal.Profiles;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

// PATCH carries "not sent" as null and the write model keeps saying it: an empty contact list
// removes them all.
public class PersonalProfileMapperShould
{
    [Fact]
    public void KeepAnUnsentContactListNull()
    {
        var update = PersonalProfileMapper.ToUpdateUser(new UpdateProfile
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
        var update = PersonalProfileMapper.ToUpdateUser(new UpdateProfile
        {
            Contacts = []
        });

        update.Contacts.Should().NotBeNull().And.BeEmpty(
            "an empty list is an instruction to remove all contacts");
    }

    [Fact]
    public void NumberContactsInSubmittedOrder()
    {
        var update = PersonalProfileMapper.ToUpdateUser(new UpdateProfile
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

    // A visibility block whose flag was not sent folds to null - the lifted negation must not
    // invent a setting.
    [Fact]
    public void FoldAPartialVisibilityBlockWithoutInventingValues()
    {
        var update = PersonalProfileMapper.ToUpdateUser(new UpdateProfile
        {
            Visibility = new UpdateVisibilitySettings { ShowBirthday = false }
        });

        update.ShowBirthday.Should().BeFalse();
        update.RatingDisabled.Should().BeNull("showRating was not sent");
    }

    // The participation in the rating is saved by the profile: a hidden rating is a disabled one on
    // the account.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void CarryTheParticipationInTheRating(bool showRating, bool ratingDisabled) =>
        PersonalProfileMapper.ToUpdateUser(new UpdateProfile
        {
            Visibility = new UpdateVisibilitySettings { ShowRating = showRating }
        }).RatingDisabled.Should().Be(ratingDisabled);
}
