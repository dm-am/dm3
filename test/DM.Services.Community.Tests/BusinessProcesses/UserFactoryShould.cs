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
    public void CreateNewUser()
    {
        var userId = Guid.NewGuid();
        newIdSetup.Returns(userId);
        var rightNow = new DateTimeOffset(2019, 05, 12, 11, 07, 10, TimeSpan.Zero);
        currentMomentSetup.Returns(rightNow);
        var actual = factory.Create(new UserRegistration
        {
            Email = "email  ",
            Login = "   login",
            Password = "whatever"
        }, "salt", "hash", 2);

        actual.Should().BeEquivalentTo(new User
        {
            UserId = userId,
            Email = "email",
            Login = "login",
            Salt = "salt",
            PasswordHash = "hash",
            Activated = false,
            LastActivityUtc = null,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified,
            QualityRating = 0,
            QuantityRating = 0,
            RatingDisabled = false,
            CanMerge = false,
            MergeRequested = null,
            IsRemoved = false,
            CreatedUtc = rightNow,
            PasswordHashVersion = 2,
            // String fields initialized by factory
            Status = string.Empty,
            Name = string.Empty,
            Location = string.Empty,
            Icq = string.Empty,
            Skype = string.Empty,
            Info = string.Empty,
            ProfilePictureUrl = string.Empty,
            SmallProfilePictureUrl = string.Empty,
            MediumProfilePictureUrl = string.Empty,
#pragma warning disable CS0618
            TimezoneId = "UTC"
#pragma warning restore CS0618
        });
    }
}