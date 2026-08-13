using System.Linq;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.Profiles;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Profiles;

public class UpdateUserValidatorShould : UnitTestBase
{
    private readonly UpdateUserValidator validator = new();

    private static UserSettings ValidSettings() => new()
    {
        Paging = new PagingSettings
        {
            CommentsPerPage = 10,
            MessagesPerPage = 10,
            PostsPerPage = 10,
            TopicsPerPage = 10,
            EntitiesPerPage = 10
        }
    };

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateUser
        {
            Username = "user",
            Status = "Playing",
            Name = "Real Name",
            Location = "Somewhere",
            Contacts = [new UserContact { ContactType = "Telegram", ContactValue = "@user" }],
            Settings = ValidSettings()
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PassWhenOptionalFieldsAreOmitted()
    {
        var input = new UpdateUser
        {
            Username = "user",
            Status = null!,
            Name = null!,
            Location = null!,
            Contacts = null!,
            Settings = null!
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenStatusExceedsMaxLength()
    {
        var input = new UpdateUser { Status = new string('a', 201) };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(u => u.Status)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenNameExceedsMaxLength()
    {
        var input = new UpdateUser { Name = new string('a', 101) };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(u => u.Name)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenLocationExceedsMaxLength()
    {
        var input = new UpdateUser { Location = new string('a', 101) };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(u => u.Location)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenThereAreTooManyContacts()
    {
        var input = new UpdateUser
        {
            Contacts = Enumerable.Range(0, 11)
                .Select(i => new UserContact { ContactType = "Type", ContactValue = $"value{i}" })
                .ToArray()
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(u => u.Contacts)
            .WithErrorMessage(ValidationError.TooMany);
    }

    [Fact]
    public void FailWhenContactTypeIsEmpty()
    {
        var input = new UpdateUser
        {
            Contacts = [new UserContact { ContactType = "", ContactValue = "@user" }]
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("Contacts[0].ContactType")
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenContactValueExceedsMaxLength()
    {
        var input = new UpdateUser
        {
            Contacts = [new UserContact { ContactType = "Telegram", ContactValue = new string('a', 201) }]
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor("Contacts[0].ContactValue")
            .WithErrorMessage(ValidationError.Long);
    }

    /// <summary>
    /// The page size is one of the sizes the account form offers, not a number in
    /// a range: 15 sits inside any plausible range and no list on the site is
    /// paged by it.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(201)]
    public void FailWhenCommentsPerPageIsNotAnOfferedSize(int value)
    {
        var settings = ValidSettings();
        settings.Paging.CommentsPerPage = value;
        var input = new UpdateUser { Settings = settings };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(u => u.Settings.Paging.CommentsPerPage)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(201)]
    public void FailWhenPostsPerPageIsNotAnOfferedSize(int value)
    {
        var settings = ValidSettings();
        settings.Paging.PostsPerPage = value;
        var input = new UpdateUser { Settings = settings };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(u => u.Settings.Paging.PostsPerPage)
            .WithErrorMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// The largest offered size saves. It used to fail the whole form — the
    /// validator demanded less than 200 while the form offered exactly 200 — so a
    /// reader who picked it could not save an unrelated change either.
    /// </summary>
    [Fact]
    public void AcceptTheLargestOfferedPageSize()
    {
        var settings = ValidSettings();
        settings.Paging.CommentsPerPage = PagingPolicy.MaxPageSize;
        settings.Paging.PostsPerPage = PagingPolicy.MaxPageSize;
        var input = new UpdateUser { Settings = settings };

        var result = validator.TestValidate(input);

        result.ShouldNotHaveValidationErrorFor(u => u.Settings.Paging.CommentsPerPage);
        result.ShouldNotHaveValidationErrorFor(u => u.Settings.Paging.PostsPerPage);
    }
}
