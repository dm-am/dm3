using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation.Security;
using DM.Services.Community.BusinessProcesses.Account.EmailChange;
using DM.Services.Core.Exceptions;
using DM.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Services.Community.Tests.BusinessProcesses;

public class EmailChangeValidatorShould : UnitTestBase
{
    private readonly UserEmailChangeValidator validator;
    private readonly Mock<IEmailChangeRepository> emailChangeRepository;
    private readonly Mock<ISecurityManager> securityManager;

    public EmailChangeValidatorShould()
    {
        emailChangeRepository = Mock<IEmailChangeRepository>();
        securityManager = Mock<ISecurityManager>();
        validator = new UserEmailChangeValidator(emailChangeRepository.Object, securityManager.Object);
    }

    [Fact]
    public async Task FailWhenLoginIsEmpty()
    {
        var model = new UserEmailChange
        {
            Login = "",
            Password = "password",
            Email = "new@test.com"
        };

        var result = await validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Login" && e.ErrorMessage == ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenUserNotFound()
    {
        emailChangeRepository
            .Setup(r => r.FindUser("nonexistent"))
            .ReturnsAsync((AuthenticatedUser?)null!);

        var model = new UserEmailChange
        {
            Login = "nonexistent",
            Password = "password",
            Email = "new@test.com"
        };

        var result = await validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Login" && e.ErrorMessage == ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenPasswordIsEmpty()
    {
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Login = "testuser",
            Email = "old@test.com",
            Salt = "salt",
            PasswordHash = "hash",
            PasswordHashVersion = 2
        };

        emailChangeRepository
            .Setup(r => r.FindUser("testuser"))
            .ReturnsAsync(user);

        var model = new UserEmailChange
        {
            Login = "testuser",
            Password = "",
            Email = "new@test.com"
        };

        var result = await validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Password" && e.ErrorMessage == ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenPasswordDoesNotMatch()
    {
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Login = "testuser",
            Email = "old@test.com",
            Salt = "salt",
            PasswordHash = "hash",
            PasswordHashVersion = 2
        };

        emailChangeRepository
            .Setup(r => r.FindUser("testuser"))
            .ReturnsAsync(user);

        securityManager
            .Setup(s => s.ComparePasswords("wrongpassword", "salt", "hash", 2))
            .Returns(false);

        var model = new UserEmailChange
        {
            Login = "testuser",
            Password = "wrongpassword",
            Email = "new@test.com"
        };

        var result = await validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Password" && e.ErrorMessage == ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenEmailIsEmpty()
    {
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Login = "testuser",
            Email = "old@test.com",
            Salt = "salt",
            PasswordHash = "hash",
            PasswordHashVersion = 2
        };

        emailChangeRepository
            .Setup(r => r.FindUser("testuser"))
            .ReturnsAsync(user);

        securityManager
            .Setup(s => s.ComparePasswords("password", "salt", "hash", 2))
            .Returns(true);

        var model = new UserEmailChange
        {
            Login = "testuser",
            Password = "password",
            Email = ""
        };

        var result = await validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Email" && e.ErrorMessage == ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenEmailFormatIsInvalid()
    {
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Login = "testuser",
            Email = "old@test.com",
            Salt = "salt",
            PasswordHash = "hash",
            PasswordHashVersion = 2
        };

        emailChangeRepository
            .Setup(r => r.FindUser("testuser"))
            .ReturnsAsync(user);

        securityManager
            .Setup(s => s.ComparePasswords("password", "salt", "hash", 2))
            .Returns(true);

        var model = new UserEmailChange
        {
            Login = "testuser",
            Password = "password",
            Email = "not-an-email"
        };

        var result = await validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Email" && e.ErrorMessage == ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenEmailIsSameAsCurrent()
    {
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Login = "testuser",
            Email = "old@test.com",
            Salt = "salt",
            PasswordHash = "hash",
            PasswordHashVersion = 2
        };

        emailChangeRepository
            .Setup(r => r.FindUser("testuser"))
            .ReturnsAsync(user);

        securityManager
            .Setup(s => s.ComparePasswords("password", "salt", "hash", 2))
            .Returns(true);

        var model = new UserEmailChange
        {
            Login = "testuser",
            Password = "password",
            Email = "old@test.com"
        };

        var result = await validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Email" && e.ErrorMessage == ValidationError.Unchanged);
    }

    [Fact]
    public async Task FailWhenEmailIsSameAsCurrent_CaseInsensitive()
    {
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Login = "testuser",
            Email = "Old@Test.com",
            Salt = "salt",
            PasswordHash = "hash",
            PasswordHashVersion = 2
        };

        emailChangeRepository
            .Setup(r => r.FindUser("testuser"))
            .ReturnsAsync(user);

        securityManager
            .Setup(s => s.ComparePasswords("password", "salt", "hash", 2))
            .Returns(true);

        var model = new UserEmailChange
        {
            Login = "testuser",
            Password = "password",
            Email = "old@test.com"
        };

        var result = await validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Email" && e.ErrorMessage == ValidationError.Unchanged);
    }

    [Fact]
    public async Task FailWhenEmailAlreadyTaken()
    {
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Login = "testuser",
            Email = "old@test.com",
            Salt = "salt",
            PasswordHash = "hash",
            PasswordHashVersion = 2
        };

        emailChangeRepository
            .Setup(r => r.FindUser("testuser"))
            .ReturnsAsync(user);

        securityManager
            .Setup(s => s.ComparePasswords("password", "salt", "hash", 2))
            .Returns(true);

        emailChangeRepository
            .Setup(r => r.IsEmailFree("taken@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var model = new UserEmailChange
        {
            Login = "testuser",
            Password = "password",
            Email = "taken@test.com"
        };

        var result = await validator.ValidateAsync(model);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Email" && e.ErrorMessage == ValidationError.Taken);
    }

    [Fact]
    public async Task PassWithValidData()
    {
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Login = "testuser",
            Email = "old@test.com",
            Salt = "salt",
            PasswordHash = "hash",
            PasswordHashVersion = 2
        };

        emailChangeRepository
            .Setup(r => r.FindUser("testuser"))
            .ReturnsAsync(user);

        securityManager
            .Setup(s => s.ComparePasswords("password", "salt", "hash", 2))
            .Returns(true);

        emailChangeRepository
            .Setup(r => r.IsEmailFree("new@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var model = new UserEmailChange
        {
            Login = "testuser",
            Password = "password",
            Email = "new@test.com"
        };

        var result = await validator.ValidateAsync(model);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
