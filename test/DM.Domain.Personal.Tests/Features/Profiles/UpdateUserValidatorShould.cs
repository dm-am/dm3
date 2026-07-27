using System.Linq;
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

    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    public void FailWhenCommentsPerPageIsOutOfRange(int value)
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
    [InlineData(200)]
    public void FailWhenPostsPerPageIsOutOfRange(int value)
    {
        var settings = ValidSettings();
        settings.Paging.PostsPerPage = value;
        var input = new UpdateUser { Settings = settings };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(u => u.Settings.Paging.PostsPerPage)
            .WithErrorMessage(ValidationError.Invalid);
    }
}
