using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.Blacklists;
using DM.Testing;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Blacklists;

public class OperateUserBlacklistLinkValidatorShould : UnitTestBase
{
    private readonly OperateUserBlacklistLinkValidator validator;
    private readonly Mock<IUserLookupService> userLookupServiceMock;

    public OperateUserBlacklistLinkValidatorShould()
    {
        userLookupServiceMock = Mock<IUserLookupService>();
        validator = new OperateUserBlacklistLinkValidator(userLookupServiceMock.Object);
    }

    [Fact]
    public async Task PassWhenUsernameExists()
    {
        userLookupServiceMock
            .Setup(s => s.UserExistsAsync("existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var input = new OperateUserBlacklistLink { Username = "existing" };

        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenUsernameIsEmpty()
    {
        var input = new OperateUserBlacklistLink { Username = "" };

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

        var input = new OperateUserBlacklistLink { Username = "nonexistent" };

        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(r => r.Username)
            .WithErrorMessage(ValidationError.Invalid);
    }
}
