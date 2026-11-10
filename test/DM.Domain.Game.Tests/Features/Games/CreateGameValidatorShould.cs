using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Games;
using DM.Testing;
using DM.Testing.Dsl;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class CreateGameValidatorShould : UnitTestBase
{
    private readonly CreateGameValidator validator;
    private readonly IUserLookupService userLookupService;
    private readonly IUserBlacklistChecker blacklistChecker;
    private readonly Guid masterId = Guid.NewGuid();

    public CreateGameValidatorShould()
    {
        userLookupService = Mock<IUserLookupService>();
        blacklistChecker = Mock<IUserBlacklistChecker>();
        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(masterId, UserRole.RegularUser));
        validator = new CreateGameValidator(userLookupService, blacklistChecker, identityProvider);
    }

    private static CreateGame ValidGame(Action<CreateGame>? change = null)
    {
        var game = new CreateGame
        {
            Title = "Valid Game Title",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = new string('a', GameFieldLimits.InfoMinLength)
        };
        change?.Invoke(game);
        return game;
    }

    [Fact]
    public async Task PassForValidInput()
    {
        var input = ValidGame();

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenCommentsAccessModeIsNotInEnum()
    {
        var input = ValidGame(g => g.CommentsAccessMode = (CommentsAccessMode)30000);

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.CommentsAccessMode)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenTitleIsEmpty()
    {
        var input = ValidGame(g => g.Title = "");

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenTitleExceedsMaxLength()
    {
        var input = ValidGame(g => g.Title = new string('a', GameFieldLimits.TitleMaxLength + 1));

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(ValidationError.Long);
    }

    // Checking only one over the limit cannot see a validator stricter than the contract.
    [Fact]
    public async Task AcceptFieldsFilledToTheLimitTheContractPublishes()
    {
        var input = ValidGame(g =>
        {
            g.Title = new string('a', GameFieldLimits.TitleMaxLength);
            g.SystemName = new string('b', GameFieldLimits.SystemMaxLength);
            g.NarrativeSetting = new string('c', GameFieldLimits.SettingMaxLength);
        });

        var result = await validator.TestValidateAsync(input);

        result.ShouldNotHaveValidationErrorFor(x => x.Title);
        result.ShouldNotHaveValidationErrorFor(x => x.SystemName);
        result.ShouldNotHaveValidationErrorFor(x => x.NarrativeSetting);
        result.ShouldNotHaveValidationErrorFor(x => x.Info);
    }

    [Fact]
    public async Task FailWhenSystemNameIsEmpty()
    {
        var input = ValidGame(g => g.SystemName = "");

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.SystemName)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenSystemNameExceedsMaxLength()
    {
        var input = ValidGame(g => g.SystemName = new string('a', GameFieldLimits.SystemMaxLength + 1));

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.SystemName)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task FailWhenNarrativeSettingIsEmpty()
    {
        var input = ValidGame(g => g.NarrativeSetting = "");

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.NarrativeSetting)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenNarrativeSettingExceedsMaxLength()
    {
        var input = ValidGame(g => g.NarrativeSetting = new string('a', GameFieldLimits.SettingMaxLength + 1));

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.NarrativeSetting)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task FailWhenInfoIsEmpty()
    {
        var input = ValidGame(g => g.Info = "");

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Info)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenInfoIsTooShort()
    {
        var input = ValidGame(g => g.Info = new string('a', GameFieldLimits.InfoMinLength - 1));

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Info)
            .WithErrorMessage(ValidationError.Short);
    }

    [Fact]
    public async Task FailWhenAssistantUsernameDoesNotExist()
    {
        userLookupService
            .UsernameExistsAsync("nonexistent", Arg.Any<CancellationToken>()).Returns(false);

        var input = ValidGame(g => g.AssistantUsername = "nonexistent");

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.AssistantUsername)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task PassWhenAssistantUsernameExists()
    {
        userLookupService
            .UsernameExistsAsync("validassistant", Arg.Any<CancellationToken>()).Returns(true);

        var input = ValidGame(g => g.AssistantUsername = "validassistant");

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // The invitation after creation would be refused, and by then the game would exist without it.
    [Fact]
    public async Task FailWhenTheAssistantKeepsTheMasterInTheirBlacklist()
    {
        var assistantId = Guid.NewGuid();
        userLookupService
            .UsernameExistsAsync("blocker", Arg.Any<CancellationToken>()).Returns(true);
        userLookupService
            .FindUserIdAsync("blocker", Arg.Any<CancellationToken>()).Returns((true, assistantId));
        blacklistChecker
            .IsBlockedAsync(assistantId, masterId, Arg.Any<CancellationToken>()).Returns(true);

        var input = ValidGame(g => g.AssistantUsername = "blocker");

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.AssistantUsername)
            .WithErrorMessage(RefusalMessage.CannotInviteThisUser);
    }

    // The sheet is created with the game, so a set the sheet cannot hold refuses the whole request
    // - and there is no game left over to repair.
    [Fact]
    public async Task FailWhenASpecificationCarriesNoTitle()
    {
        var input = ValidGame(g => g.AttributeSpecifications =
            [new CreateAttributeSpecification { Title = "", Type = AttributeSpecificationType.Text }]);

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor("AttributeSpecifications[0].Title")
            .WithErrorMessage("Введите название атрибута");
    }

    // A schema-level rule, checked over the whole submitted set rather than over one specification
    // at a time.
    [Fact]
    public async Task FailWhenTwoSpecificationsClaimToBeTheDescriptor()
    {
        var input = ValidGame(g => g.AttributeSpecifications =
        [
            new CreateAttributeSpecification
            {
                Title = "Раса", Type = AttributeSpecificationType.Text, IsDescriptor = true
            },
            new CreateAttributeSpecification
            {
                Title = "Класс", Type = AttributeSpecificationType.Text, IsDescriptor = true, Order = 1
            }
        ]);

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(g => g.AttributeSpecifications);
    }

    // An empty sheet is a master's choice, not a refusal: characters of that game simply carry no
    // attributes.
    [Fact]
    public async Task PassWhenNoSpecificationsAreSubmitted()
    {
        var input = ValidGame(g => g.AttributeSpecifications = []);

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
