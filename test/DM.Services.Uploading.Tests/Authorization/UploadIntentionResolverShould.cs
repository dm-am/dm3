using System;
using DM.Services.Authentication.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Uploading.Authorization;
using FluentAssertions;
using Xunit;

namespace DM.Services.Uploading.Tests.Authorization;

public class UploadIntentionResolverShould
{
    private readonly UploadIntentionResolver resolver;

    public UploadIntentionResolverShould()
    {
        resolver = new UploadIntentionResolver();
    }

    [Fact]
    public void AllowListAll_When_Admin()
    {
        // Arrange
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Admin
        };

        // Act
        var result = resolver.IsAllowed(user, UploadIntention.ListAll);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    public void DenyListAll_When_NotAdmin(UserRole role)
    {
        // Arrange
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Role = role
        };

        // Act
        var result = resolver.IsAllowed(user, UploadIntention.ListAll);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void DenyListAll_When_Anonymous()
    {
        // Arrange
        var user = AuthenticatedUser.Guest;

        // Act
        var result = resolver.IsAllowed(user, UploadIntention.ListAll);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AllowListUser_When_Admin()
    {
        // Arrange
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Admin
        };

        // Act
        var result = resolver.IsAllowed(user, UploadIntention.ListUser);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.SeniorModerator)]
    public void DenyListUser_When_NotAdmin(UserRole role)
    {
        // Arrange
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Role = role
        };

        // Act
        var result = resolver.IsAllowed(user, UploadIntention.ListUser);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void DenyListUser_When_Anonymous()
    {
        // Arrange
        var user = AuthenticatedUser.Guest;

        // Act
        var result = resolver.IsAllowed(user, UploadIntention.ListUser);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void DenyUnknownIntention()
    {
        // Arrange
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Admin
        };

        // Act - View and Delete are not handled by this resolver (no target)
        var result = resolver.IsAllowed(user, UploadIntention.View);

        // Assert
        result.Should().BeFalse();
    }
}
