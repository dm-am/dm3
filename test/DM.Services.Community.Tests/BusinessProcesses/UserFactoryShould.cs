using System;
using DM.Services.Community.BusinessProcesses.Account.Registration;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Tests.Core;
using FluentAssertions;
using Moq.Language.Flow;
using Xunit;

namespace DM.Services.Community.Tests.BusinessProcesses;

public class UserFactoryShould : UnitTestBase
{
    private readonly ISetup<IGuidFactory, Guid> newIdSetup;
    private readonly ISetup<IDateTimeProvider, DateTimeOffset> currentMomentSetup;
    private readonly UserFactory factory;

    public UserFactoryShould()
    {
        var guidFactory = Mock<IGuidFactory>();
        newIdSetup = guidFactory.Setup(f => f.Create());
        var dateTimeProvider = Mock<IDateTimeProvider>();
        currentMomentSetup = dateTimeProvider.Setup(p => p.Now);
        factory = new UserFactory(guidFactory.Object, dateTimeProvider.Object);
    }

    [Fact]
    public void CreateNewUserFromPendingRegistration()
    {
        var userId = Guid.NewGuid();
        newIdSetup.Returns(userId);
        var rightNow = new DateTimeOffset(2019, 05, 12, 11, 07, 10, TimeSpan.Zero);
        currentMomentSetup.Returns(rightNow);

        var pending = new PendingRegistration
        {
            Email = "email@test.com",
            PasswordHash = "hash",
            Salt = "salt",
            PasswordHashVersion = 2,
            AcceptedRules = true
        };

        var actual = factory.CreateFromPending(pending, "  TestLogin  ");

        actual.Should().BeEquivalentTo(new User
        {
            UserId = userId,
            Email = "email@test.com",
            Login = "TestLogin",
            Salt = "salt",
            PasswordHash = "hash",
            LastActivityUtc = rightNow, // Set on activation so user appears in active list
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified,
            QualityRating = 0,
            QuantityRating = 0,
            RatingDisabled = false,
            IsRemoved = false,
            CreatedUtc = rightNow,
            PasswordHashVersion = 2,
            // String fields initialized by factory
            Status = string.Empty,
            Name = string.Empty,
            Location = string.Empty,
            Icq = string.Empty,
            Skype = string.Empty,
            Info = string.Empty
        });
    }

    [Fact]
    public void TrimLoginWhenCreatingFromPending()
    {
        var userId = Guid.NewGuid();
        newIdSetup.Returns(userId);
        currentMomentSetup.Returns(DateTimeOffset.UtcNow);

        var pending = new PendingRegistration
        {
            Email = "email@test.com",
            PasswordHash = "hash",
            Salt = "salt",
            PasswordHashVersion = 2
        };

        var actual = factory.CreateFromPending(pending, "   spacedLogin   ");

        actual.Login.Should().Be("spacedLogin");
    }

}
