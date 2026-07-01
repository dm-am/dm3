using System;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Subscription notification settings (flags).
/// </summary>
/// <remarks>
/// The same bit positions carry different semantics depending on the
/// subscription's <see cref="SubscriptionTargetType"/>:
/// <list type="bullet">
///   <item>For <b>Game</b>/<b>Blog</b>/<b>Topic</b> targets — the
///   "per-entity" flags (<see cref="NewPosts"/>,
///   <see cref="NewPublications"/>, <see cref="NewComments"/>,
///   <see cref="StatusChanges"/>, <see cref="CharacterUpdates"/>) apply.</item>
///   <item>For <b>User</b> targets — the "per-author-category" flags
///   (<see cref="AuthorGameEvents"/>, <see cref="AuthorBlogEvents"/>,
///   <see cref="AuthorTopicEvents"/>) gate the three customizable
///   buckets the user can toggle in the subscribe popover.</item>
/// </list>
/// Channel flags (<see cref="InApp"/>, <see cref="Email"/>) are
/// orthogonal and apply to all target types.
/// </remarks>
[Flags]
public enum SubscriptionSettings
{
    /// <summary>
    /// No notifications
    /// </summary>
    None = 0,

    #region Per-entity content flags (Game / Blog / Topic targets)

    /// <summary>
    /// Notify about new posts in subscribed game.
    /// </summary>
    NewPosts = 1 << 0,

    /// <summary>
    /// Notify about new publications in subscribed blog.
    /// </summary>
    NewPublications = 1 << 1,

    /// <summary>
    /// Notify about new comments in subscribed discussion (topic / game / blog).
    /// </summary>
    NewComments = 1 << 2,

    /// <summary>
    /// Notify about new topics in subscribed board.
    /// </summary>
    NewTopics = 1 << 3,

    /// <summary>
    /// Notify about status changes (game/blog activated, closed, …).
    /// </summary>
    StatusChanges = 1 << 4,

    /// <summary>
    /// Notify about character updates in subscribed game.
    /// </summary>
    CharacterUpdates = 1 << 5,

    // Bit 1 << 6 is intentionally left vacant — the previous
    // AuthorNewContent catch-all flag has been replaced by the three
    // category-specific flags below (AuthorGameEvents / AuthorBlogEvents /
    // AuthorTopicEvents). Any subscription rows that still carry bit 6 must
    // be migrated to the new flags before this enum drifts further.

    #endregion

    #region Notification channels

    /// <summary>
    /// Send in-app notifications.
    /// </summary>
    InApp = 1 << 7,

    /// <summary>
    /// Send email notifications.
    /// </summary>
    Email = 1 << 8,

    #endregion

    #region Per-author-category flags (User target only)

    /// <summary>
    /// Game-related events from the subscribed author:
    /// new game with public-draft visibility, game becomes Active,
    /// game recruitment opens / subsequent recruitment.
    /// </summary>
    AuthorGameEvents = 1 << 9,

    /// <summary>
    /// Blog-related events from the subscribed author:
    /// new blog with public-draft visibility, blog becomes Active.
    /// </summary>
    AuthorBlogEvents = 1 << 10,

    /// <summary>
    /// Forum-topic events from the subscribed author: new topic.
    /// </summary>
    AuthorTopicEvents = 1 << 11,

    #endregion

    #region Common presets

    /// <summary>
    /// Default settings for game reader subscription.
    /// </summary>
    GameReaderDefault = NewPosts | StatusChanges | InApp,

    /// <summary>
    /// Default settings for game player subscription.
    /// </summary>
    GamePlayerDefault = NewPosts | StatusChanges | CharacterUpdates | InApp | Email,

    /// <summary>
    /// Default settings for blog reader subscription.
    /// </summary>
    BlogReaderDefault = NewPublications | InApp,

    /// <summary>
    /// Default settings for topic subscription.
    /// </summary>
    TopicDefault = NewComments | InApp,

    /// <summary>
    /// Default settings for user subscription — all three author-category
    /// channels on, in-app delivery on. User can toggle the three category
    /// flags via the subscribe popover.
    /// </summary>
    UserSubscriptionDefault = AuthorGameEvents | AuthorBlogEvents | AuthorTopicEvents | InApp,

    #endregion
}
