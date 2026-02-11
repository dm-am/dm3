using System;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Common.Authorization;
using DM.Services.Core.Caching;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Tests.Core;
using FluentAssertions;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace DM.Services.Community.Tests.BusinessProcesses;

public class UserReadingServiceShould : UnitTestBase
{
    private readonly ISetup<IIdentity, UserSettings> currentUserSettingsSetup;
    private readonly Mock<IUserReadingRepository> readingRepository;
    private readonly UserReadingService service;

    public UserReadingServiceShould()
    {
        var identityProvider = Mock<IIdentityProvider>();
        var identity = Mock<IIdentity>();
        identityProvider
            .Setup(p => p.Current)
            .Returns(identity.Object);
        currentUserSettingsSetup = identity.Setup(i => i.Settings);

        readingRepository = Mock<IUserReadingRepository>();
        var intentionManager = Mock<IIntentionManager>();
        var cache = Mock<ICache>();
        // Configure cache to invoke the factory function (pass-through)
        cache
            .Setup(c => c.GetOrCreate(It.IsAny<object>(), It.IsAny<Func<Task<UserDetails>>>(), It.IsAny<TimeSpan>()))
            .Returns((object key, Func<Task<UserDetails>> factory, TimeSpan ttl) => factory());

        service = new UserReadingService(identityProvider.Object, readingRepository.Object, intentionManager.Object, cache.Object);
    }

    [Fact]
    public async Task ThrowGoneWhenUserDetailsNotFound()
    {
        readingRepository
            .Setup(r => r.GetUserDetails(It.IsAny<string>()))
            .ReturnsAsync((UserDetails?)null!);

        (await service.Awaiting(s => s.GetDetails("User"))
            .Should().ThrowAsync<HttpException>())
            .And.StatusCode.Should().Be(HttpStatusCode.Gone);
        readingRepository.Verify(r => r.GetUserDetails("User"), Times.Once);
    }

    [Fact]
    public async Task ReturnFoundUserDetails()
    {
        var expected = new UserDetails();
        readingRepository
            .Setup(r => r.GetUserDetails(It.IsAny<string>()))
            .ReturnsAsync(expected);

        var actual = await service.GetDetails("User");

        actual.Should().Be(expected);
        readingRepository.Verify(r => r.GetUserDetails("User"), Times.Once);
    }

    [Fact]
    public async Task ThrowGoneWhenUserNotFound()
    {
        readingRepository
            .Setup(r => r.GetUser(It.IsAny<string>()))
            .ReturnsAsync((GeneralUser?)null!);

        (await service.Awaiting(s => s.Get("User"))
            .Should().ThrowAsync<HttpException>())
            .And.StatusCode.Should().Be(HttpStatusCode.Gone);
        readingRepository.Verify(r => r.GetUser("User"), Times.Once);
    }

    [Fact]
    public async Task ReturnFoundUser()
    {
        var expected = new GeneralUser();
        readingRepository
            .Setup(r => r.GetUser(It.IsAny<string>()))
            .ReturnsAsync(expected);

        var actual = await service.Get("User");

        actual.Should().Be(expected);
        readingRepository.Verify(r => r.GetUser("User"), Times.Once);
    }

    [Fact]
    public async Task ReturnFetchedUsers()
    {
        var expected = new GeneralUser[0];
        readingRepository
            .Setup(r => r.CountUsers(It.IsAny<UserActivityFilter>(), It.IsAny<string>()))
            .ReturnsAsync(10);
        readingRepository
            .Setup(r => r.GetUsers(It.IsAny<PagingData>(), It.IsAny<UserActivityFilter>(), It.IsAny<string>()))
            .ReturnsAsync(expected);
        currentUserSettingsSetup.Returns(new UserSettings{Paging = new PagingSettings{EntitiesPerPage = 10}});

        var (actual, _) = await service.Get(new PagingQuery(), UserActivityFilter.Active);
        actual.Should().BeSameAs(expected);
        readingRepository.Verify(r => r.CountUsers(UserActivityFilter.Active, null), Times.Once);
        readingRepository.Verify(r => r.GetUsers(It.IsAny<PagingData>(), UserActivityFilter.Active, null));
    }
}