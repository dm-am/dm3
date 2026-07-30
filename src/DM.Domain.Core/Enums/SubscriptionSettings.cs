using System;

namespace DM.Domain.Core.Enums;

/// <summary>
/// Subscription notification settings (flags).
/// </summary>
/// <remarks>
/// The three groups occupy disjoint bit ranges: no position ever carries two
/// meanings, so a stored value is readable without knowing the subscription's
/// <see cref="SubscriptionTargetType"/>.
/// <list type="bullet">
///   <item>Bits 0-5 — the "per-entity" flags (<see cref="NewPosts"/>,
///   <see cref="NewPublications"/>, <see cref="NewComments"/>,
///   <see cref="NewTopics"/>, <see cref="StatusChanges"/>,
///   <see cref="CharacterUpdates"/>), read for <b>Game</b>/<b>Blog</b>/<b>Topic</b>
///   targets.</item>
///   <item>Bits 7-8 — the channel flags (<see cref="InApp"/>,
///   <see cref="Email"/>), orthogonal and read for every target type.</item>
///   <item>Bits 9-11 — the "per-author-category" flags
///   (<see cref="AuthorGameEvents"/>, <see cref="AuthorBlogEvents"/>,
///   <see cref="AuthorTopicEvents"/>), read for <b>User</b> targets: they gate
///   the three customizable buckets the user can toggle in the subscribe
///   popover.</item>
/// </list>
/// A flag added later takes a free high bit. Reusing a low one would make the
/// value depend on the target type, which is exactly what this layout avoids.
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

    // Bit 1 << 6 is vacant: it held the AuthorNewContent catch-all, now split
    // into the three category flags below (AuthorGameEvents / AuthorBlogEvents /
    // AuthorTopicEvents). It stays vacant instead of being recycled — a bit
    // that once meant something else is the one way this layout could end up
    // needing the target type to be read.

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
