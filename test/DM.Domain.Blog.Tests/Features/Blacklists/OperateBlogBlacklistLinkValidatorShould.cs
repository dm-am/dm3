using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Testing;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Blog.Tests.Features.Blacklists;

public class OperateBlogBlacklistLinkValidatorShould : UnitTestBase
{
    private readonly OperateBlogBlacklistLinkValidator validator;
    private readonly Mock<IUserLookupService> userLookupServiceMock;

    public OperateBlogBlacklistLinkValidatorShould()
    {
        userLookupServiceMock = Mock<IUserLookupService>();
        validator = new OperateBlogBlacklistLinkValidator(userLookupServiceMock.Object);
    }

    [Fact]
    public async Task PassForValidInput()
    {
        userLookupServiceMock
            .Setup(s => s.UserExistsAsync("existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var input = new OperateBlogBlacklistLink
        {
            BlogId = Guid.NewGuid(),
            Username = "existing"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenBlogIdIsEmpty()
    {
        userLookupServiceMock
            .Setup(s => s.UserExistsAsync("existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var input = new OperateBlogBlacklistLink
        {
            BlogId = Guid.Empty,
            Username = "existing"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(r => r.BlogId)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenUsernameIsEmpty()
    {
        var input = new OperateBlogBlacklistLink
        {
            BlogId = Guid.NewGuid(),
            Username = ""
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(r => r.Username)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenUsernameDoesNotExist()
    {
        userLookupServiceMock
            .Setup(s => s.UserExistsAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var input = new OperateBlogBlacklistLink
        {
            BlogId = Guid.NewGuid(),
            Username = "nonexistent"
        };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(r => r.Username)
            .WithErrorMessage(ValidationError.Invalid);
    }
}
