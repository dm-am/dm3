using System.Threading;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Account.Registration;
using DM.Services.Community.Configuration;
using DM.Tests.Core;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Services.Community.Tests.BusinessProcesses;

public class RegistrationValidatorShould : UnitTestBase
{
    private readonly UserRegistrationValidator validator;
    private readonly Mock<IRegistrationRepository> registrationRepository;

    public RegistrationValidatorShould()
    {
        registrationRepository = Mock<IRegistrationRepository>(MockBehavior.Loose);
        registrationRepository
            .Setup(r => r.EmailFreeForNewRegistration("EmailTaken@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        registrationRepository
            .Setup(r => r.EmailFreeForNewRegistration(It.Is<string>(e => e != "EmailTaken@test.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var passwordPolicyOptions = Options.Create(new PasswordPolicyConfiguration());
        validator = new UserRegistrationValidator(registrationRepository.Object, passwordPolicyOptions);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    [InlineData("  ")]
    [InlineData("short")]
    public async Task ValidateUserPassword(string? password)
    {
        var userRegistration = new UserRegistration
        {
            Password = password!,
            Email = "user@email.com"
        };
        registrationRepository
            .Setup(r => r.EmailFreeForNewRegistration(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        (await validator.ValidateAsync(userRegistration)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    [InlineData("  ")]
    [InlineData("kajlsdhfaksjdhlfaksljdhfklasdhfaksdhadfasdfaslkdhfaskdjhfasldfaslkdjhaskdhfaskldfhaskjldfhaskdhfsakdhfaskldhf@gmail.com")]
    [InlineData("someInvalidEmail")]
    [InlineData("EmailTaken@test.com")]
    public async Task ValidateUserEmail(string? email)
    {
        var userRegistration = new UserRegistration
        {
            Password = "AmazingPass123",
            Email = email!
        };
        (await validator.ValidateAsync(userRegistration)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateWholeModel()
    {
        // Password must meet policy: min 10 chars, uppercase, lowercase, digit
        (await validator.ValidateAsync(new UserRegistration
        {
            Email = "my@email.com",
            Password = "AmazingPass123",
            AcceptedRules = true
        })).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task RejectExistingEmail()
    {
        var userRegistration = new UserRegistration
        {
            Email = "EmailTaken@test.com",
            Password = "AmazingPass123"
        };

        var result = await validator.ValidateAsync(userRegistration);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }
}
