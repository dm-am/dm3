using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Configuration;
using DM.Services.Authentication.Implementation;
using DM.Services.Authentication.Repositories;
using DM.Services.Core.Implementation;
using DM.Tests.Core;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Services.Authentication.Tests;

public class LoginAttemptTrackerShould : UnitTestBase
{
    private readonly Mock<ILoginAttemptRepository> repository;
    private readonly Mock<IDateTimeProvider> dateTimeProvider;
    private readonly AuthenticationConfiguration config;
    private readonly LoginAttemptTracker tracker;
    private const string TestLogin = "TestUser";

    public LoginAttemptTrackerShould()
    {
        repository = Mock<ILoginAttemptRepository>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        config = new AuthenticationConfiguration
        {
            LoginDelaySchedule = new[]
            {
                new[] { 3, 1 },
                new[] { 5, 5 },
                new[] { 10, 30 }
            },
            AccountLockoutThreshold = 15,
            AccountLockoutDurationMinutes = 30
        };

        tracker = new LoginAttemptTracker(
            repository.Object,
            dateTimeProvider.Object,
            Options.Create(config));
    }

    [Fact]
    public async Task GetDelayForUser_ReturnsZeroForLessThan3Attempts()
    {
        // Arrange
        repository.Setup(r => r.GetFailedAttemptCount(It.IsAny<string>())).ReturnsAsync(0);

        // Act
        var delay = await tracker.GetDelayForUser(TestLogin);

        // Assert
        delay.Should().Be(0);
    }

    [Fact]
    public async Task GetDelayForUser_Returns1SecondFor3Attempts()
    {
        // Arrange
        repository.Setup(r => r.GetFailedAttemptCount(It.IsAny<string>())).ReturnsAsync(3);

        // Act
        var delay = await tracker.GetDelayForUser(TestLogin);

        // Assert
        delay.Should().Be(1);
    }

    [Fact]
    public async Task GetDelayForUser_Returns1SecondFor4Attempts()
    {
        // Arrange
        repository.Setup(r => r.GetFailedAttemptCount(It.IsAny<string>())).ReturnsAsync(4);

        // Act
        var delay = await tracker.GetDelayForUser(TestLogin);

        // Assert
        delay.Should().Be(1);
    }

    [Fact]
    public async Task GetDelayForUser_Returns5SecondsFor5Attempts()
    {
        // Arrange
        repository.Setup(r => r.GetFailedAttemptCount(It.IsAny<string>())).ReturnsAsync(5);

        // Act
        var delay = await tracker.GetDelayForUser(TestLogin);

        // Assert
        delay.Should().Be(5);
    }

    [Fact]
    public async Task GetDelayForUser_Returns5SecondsFor9Attempts()
    {
        // Arrange
        repository.Setup(r => r.GetFailedAttemptCount(It.IsAny<string>())).ReturnsAsync(9);

        // Act
        var delay = await tracker.GetDelayForUser(TestLogin);

        // Assert
        delay.Should().Be(5);
    }

    [Fact]
    public async Task GetDelayForUser_Returns30SecondsFor10Attempts()
    {
        // Arrange
        repository.Setup(r => r.GetFailedAttemptCount(It.IsAny<string>())).ReturnsAsync(10);

        // Act
        var delay = await tracker.GetDelayForUser(TestLogin);

        // Assert
        delay.Should().Be(30);
    }

    [Fact]
    public async Task GetDelayForUser_Returns30SecondsFor15Attempts()
    {
        // Arrange
        repository.Setup(r => r.GetFailedAttemptCount(It.IsAny<string>())).ReturnsAsync(15);

        // Act
        var delay = await tracker.GetDelayForUser(TestLogin);

        // Assert
        delay.Should().Be(30);
    }

    [Fact]
    public async Task IsAccountLocked_ReturnsFalseWhenNotLocked()
    {
        // Arrange
        repository.Setup(r => r.GetLockoutStart(It.IsAny<string>())).ReturnsAsync((DateTime?)null);

        // Act
        var isLocked = await tracker.IsAccountLocked(TestLogin);

        // Assert
        isLocked.Should().BeFalse();
    }

    [Fact]
    public async Task IsAccountLocked_ReturnsTrueWhenLocked()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 2, 3, 10, 0, 0, TimeSpan.Zero);
        var lockoutStart = now.AddMinutes(-10).UtcDateTime; // Locked 10 minutes ago
        dateTimeProvider.Setup(d => d.Now).Returns(now);
        repository.Setup(r => r.GetLockoutStart(It.IsAny<string>())).ReturnsAsync(lockoutStart);

        // Act
        var isLocked = await tracker.IsAccountLocked(TestLogin);

        // Assert
        isLocked.Should().BeTrue();
    }

    [Fact]
    public async Task IsAccountLocked_ReturnsFalseAfterLockoutExpires()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 2, 3, 10, 0, 0, TimeSpan.Zero);
        var lockoutStart = now.AddMinutes(-31).UtcDateTime; // Locked 31 minutes ago (lockout is 30 minutes)
        dateTimeProvider.Setup(d => d.Now).Returns(now);
        repository.Setup(r => r.GetLockoutStart(It.IsAny<string>())).ReturnsAsync(lockoutStart);
        repository.Setup(r => r.ResetAttempts(It.IsAny<string>())).Returns(Task.CompletedTask);

        // Act
        var isLocked = await tracker.IsAccountLocked(TestLogin);

        // Assert
        isLocked.Should().BeFalse();
        repository.Verify(r => r.ResetAttempts(TestLogin), Times.Once);
    }

    [Fact]
    public async Task GetRemainingLockoutSeconds_Returns0WhenNotLocked()
    {
        // Arrange
        repository.Setup(r => r.GetLockoutStart(It.IsAny<string>())).ReturnsAsync((DateTime?)null);

        // Act
        var remaining = await tracker.GetRemainingLockoutSeconds(TestLogin);

        // Assert
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task GetRemainingLockoutSeconds_ReturnsCorrectRemainingTime()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 2, 3, 10, 0, 0, TimeSpan.Zero);
        var lockoutStart = now.AddMinutes(-10).UtcDateTime; // Locked 10 minutes ago
        dateTimeProvider.Setup(d => d.Now).Returns(now);
        repository.Setup(r => r.GetLockoutStart(It.IsAny<string>())).ReturnsAsync(lockoutStart);

        // Act
        var remaining = await tracker.GetRemainingLockoutSeconds(TestLogin);

        // Assert
        // 30 minute lockout - 10 minutes elapsed = 20 minutes remaining = 1200 seconds
        remaining.Should().Be(1200);
    }

    [Fact]
    public async Task GetRemainingLockoutSeconds_Returns0AfterLockoutExpires()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 2, 3, 10, 0, 0, TimeSpan.Zero);
        var lockoutStart = now.AddMinutes(-31).UtcDateTime; // Locked 31 minutes ago (lockout is 30 minutes)
        dateTimeProvider.Setup(d => d.Now).Returns(now);
        repository.Setup(r => r.GetLockoutStart(It.IsAny<string>())).ReturnsAsync(lockoutStart);

        // Act
        var remaining = await tracker.GetRemainingLockoutSeconds(TestLogin);

        // Assert
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task RecordFailedAttempt_CallsRepository()
    {
        // Arrange
        repository.Setup(r => r.RecordFailedAttempt(It.IsAny<string>())).ReturnsAsync(1);

        // Act
        await tracker.RecordFailedAttempt(TestLogin);

        // Assert
        repository.Verify(r => r.RecordFailedAttempt(TestLogin), Times.Once);
    }

    [Fact]
    public async Task RecordFailedAttempt_DoesNotLockAccountBelowThreshold()
    {
        // Arrange
        repository.Setup(r => r.RecordFailedAttempt(It.IsAny<string>())).ReturnsAsync(14);

        // Act
        await tracker.RecordFailedAttempt(TestLogin);

        // Assert
        repository.Verify(r => r.SetLockout(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task RecordFailedAttempt_LocksAccountAtThreshold()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 2, 3, 10, 0, 0, TimeSpan.Zero);
        dateTimeProvider.Setup(d => d.Now).Returns(now);
        repository.Setup(r => r.RecordFailedAttempt(It.IsAny<string>())).ReturnsAsync(15);
        repository.Setup(r => r.SetLockout(It.IsAny<string>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        // Act
        await tracker.RecordFailedAttempt(TestLogin);

        // Assert
        repository.Verify(r => r.SetLockout(TestLogin, now.UtcDateTime), Times.Once);
    }

    [Fact]
    public async Task RecordFailedAttempt_LocksAccountAboveThreshold()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 2, 3, 10, 0, 0, TimeSpan.Zero);
        dateTimeProvider.Setup(d => d.Now).Returns(now);
        repository.Setup(r => r.RecordFailedAttempt(It.IsAny<string>())).ReturnsAsync(21);
        repository.Setup(r => r.SetLockout(It.IsAny<string>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        // Act
        await tracker.RecordFailedAttempt(TestLogin);

        // Assert
        repository.Verify(r => r.SetLockout(TestLogin, now.UtcDateTime), Times.Once);
    }

    [Fact]
    public async Task ResetAttempts_CallsRepository()
    {
        // Arrange
        repository.Setup(r => r.ResetAttempts(It.IsAny<string>())).Returns(Task.CompletedTask);

        // Act
        await tracker.ResetAttempts(TestLogin);

        // Assert
        repository.Verify(r => r.ResetAttempts(TestLogin), Times.Once);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 5)]
    [InlineData(9, 5)]
    [InlineData(10, 30)]
    [InlineData(100, 30)]
    public async Task GetDelayForUser_FollowsDelaySchedule(int attempts, int expectedDelay)
    {
        // Arrange
        repository.Setup(r => r.GetFailedAttemptCount(It.IsAny<string>())).ReturnsAsync(attempts);

        // Act
        var delay = await tracker.GetDelayForUser(TestLogin);

        // Assert
        delay.Should().Be(expectedDelay);
    }

    [Fact]
    public async Task GetDelayForUser_UsesDefaultScheduleWhenNotConfigured()
    {
        // Arrange - create tracker without schedule
        var configNoSchedule = new AuthenticationConfiguration
        {
            LoginDelaySchedule = [],
            AccountLockoutThreshold = 15,
            AccountLockoutDurationMinutes = 30
        };
        var trackerNoSchedule = new LoginAttemptTracker(
            repository.Object,
            dateTimeProvider.Object,
            Options.Create(configNoSchedule));

        repository.Setup(r => r.GetFailedAttemptCount(It.IsAny<string>())).ReturnsAsync(10);

        // Act
        var delay = await trackerNoSchedule.GetDelayForUser(TestLogin);

        // Assert - default schedule: 10+ attempts = 30 seconds
        delay.Should().Be(30);
    }
}
