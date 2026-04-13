using System;
using System.Collections.Generic;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// Contract tests for permission bucket computation. Two viewers in
/// the same bucket for the same source MUST produce byte-identical
/// rendered output. These tests guarantee bucket selection follows the
/// coarsening rules so the cache remains both correct and hit-rate-efficient.
/// </summary>
public class PermissionBucketShould
{
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PostId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GameId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RoomId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // ════════════════════════════════════════════════════════════════
    // No privacy tags → coarse bucket (maximum hit-rate)
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void UseAnonymousBucket_ForGuest_WithoutPrivacyTags()
    {
        var ctx = DisplayCtx(viewer: null);
        var bucket = PermissionBucket.Compute(ctx, hasPrivacyTags: false);
        bucket.Should().Be(PermissionBucket.Anonymous);
    }

    [Fact]
    public void UseCoarseUserBucket_ForAnyAuthenticatedUser_WithoutPrivacyTags()
    {
        var ctxA = DisplayCtx(viewer: Viewer(UserA, UserRole.RegularUser));
        var ctxB = DisplayCtx(viewer: Viewer(UserB, UserRole.Moderator));

        var bucketA = PermissionBucket.Compute(ctxA, hasPrivacyTags: false);
        var bucketB = PermissionBucket.Compute(ctxB, hasPrivacyTags: false);

        bucketA.Should().Be(PermissionBucket.AuthenticatedCoarse);
        bucketB.Should().Be(PermissionBucket.AuthenticatedCoarse);
        bucketA.Should().Be(bucketB); // coarse sharing proves cache efficiency
    }

    // ════════════════════════════════════════════════════════════════
    // Audience-specific buckets
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void UseAnonymousBucket_ForPlainTextAudience()
    {
        var ctx = DisplayCtx(viewer: Viewer(UserA, UserRole.Admin)) with
        {
            Audience = RenderAudience.PlainText
        };
        PermissionBucket.Compute(ctx, hasPrivacyTags: true).Should().Be(PermissionBucket.Anonymous);
    }

    [Fact]
    public void UseAnonymousBucket_ForEmbedSafeAudience()
    {
        var ctx = DisplayCtx(viewer: Viewer(UserA, UserRole.Admin)) with
        {
            Audience = RenderAudience.EmbedSafe
        };
        PermissionBucket.Compute(ctx, hasPrivacyTags: true).Should().Be(PermissionBucket.Anonymous);
    }

    [Fact]
    public void UseAuthorEditBucket_ForAuthorEditAudience_KeyedByAuthor()
    {
        var ctx = DisplayCtx(viewer: Viewer(UserA, UserRole.RegularUser)) with
        {
            Audience = RenderAudience.AuthorEdit
        };
        var bucket = PermissionBucket.Compute(ctx, hasPrivacyTags: true);
        bucket.Key.Should().StartWith("authoredit:");
    }

    // ════════════════════════════════════════════════════════════════
    // Privacy-sensitive content → per-reason buckets
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void UsePostAuthorBucket_WhenViewerIsAuthor()
    {
        var ctx = DisplayCtx(viewer: Viewer(UserA, UserRole.RegularUser)) with
        {
            PostAuthorUserId = UserA,
            PostId = PostId
        };
        var bucket = PermissionBucket.Compute(ctx, hasPrivacyTags: true);
        bucket.Key.Should().StartWith("author:");
    }

    [Fact]
    public void UseGameLeadBucket_WhenViewerIsLead()
    {
        var ctx = DisplayCtx(viewer: Viewer(UserA, UserRole.RegularUser)) with
        {
            GameLeadUserIds = new[] { UserA },
            GameId = GameId
        };
        var bucket = PermissionBucket.Compute(ctx, hasPrivacyTags: true);
        bucket.Key.Should().StartWith("lead:");
    }

    [Fact]
    public void UseRoomSharingBucket_WhenRoomViewPrivateText()
    {
        var ctx = DisplayCtx(viewer: Viewer(UserA, UserRole.RegularUser)) with
        {
            RoomId = RoomId,
            RoomViewPrivateText = true
        };
        var bucket = PermissionBucket.Compute(ctx, hasPrivacyTags: true);
        bucket.Key.Should().StartWith("roomshare:");
    }

    [Fact]
    public void UsePostSharingBucket_WhenPostSharePrivateWithAll()
    {
        var ctx = DisplayCtx(viewer: Viewer(UserA, UserRole.RegularUser)) with
        {
            PostId = PostId,
            PostSharePrivateWithAll = true
        };
        var bucket = PermissionBucket.Compute(ctx, hasPrivacyTags: true);
        bucket.Key.Should().StartWith("postshare:");
    }

    [Fact]
    public void UseModeratorBucket_ForModeratorWithoutOtherQualification()
    {
        var ctx = DisplayCtx(viewer: Viewer(UserA, UserRole.Moderator));
        var bucket = PermissionBucket.Compute(ctx, hasPrivacyTags: true);
        bucket.Should().Be(PermissionBucket.Moderator);
    }

    [Fact]
    public void TwoViewersQualifyingThroughSameOverride_ShareBucket()
    {
        // Both viewers land in the post-sharing bucket because the post
        // has SharePrivateWithAll = true. They must share a single cache
        // entry despite being different users.
        var ctxA = DisplayCtx(viewer: Viewer(UserA, UserRole.RegularUser)) with
        {
            PostId = PostId,
            PostSharePrivateWithAll = true
        };
        var ctxB = DisplayCtx(viewer: Viewer(UserB, UserRole.Mentor)) with
        {
            PostId = PostId,
            PostSharePrivateWithAll = true
        };

        var bucketA = PermissionBucket.Compute(ctxA, hasPrivacyTags: true);
        var bucketB = PermissionBucket.Compute(ctxB, hasPrivacyTags: true);

        bucketA.Should().Be(bucketB);
    }

    // ════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════

    private static RenderContext DisplayCtx(IAuthorizationSubject? viewer) => new()
    {
        Audience = RenderAudience.Display,
        Surface = BbSurface.GamePost,
        Viewer = viewer
    };

    private static IAuthorizationSubject Viewer(Guid userId, UserRole role) => new TestSubject
    {
        UserId = userId,
        Role = role,
        IsAuthenticated = role != UserRole.Guest,
        AccessPolicy = AccessPolicy.NotSpecified
    };

    private sealed class TestSubject : IAuthorizationSubject
    {
        public Guid UserId { get; init; }
        public UserRole Role { get; init; }
        public bool IsAuthenticated { get; init; }
        public AccessPolicy AccessPolicy { get; init; }
    }
}
