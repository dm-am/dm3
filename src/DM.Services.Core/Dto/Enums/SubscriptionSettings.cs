using System;

namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Subscription notification settings (flags)
/// </summary>
[Flags]
public enum SubscriptionSettings
{
    /// <summary>
    /// No notifications
    /// </summary>
    None = 0,

    #region Content notifications

    /// <summary>
    /// Notify about new posts in game
    /// </summary>
    NewPosts = 1 << 0,

    /// <summary>
    /// Notify about new publications in blog
    /// </summary>
    NewPublications = 1 << 1,

    /// <summary>
    /// Notify about new comments in discussions
    /// </summary>
    NewComments = 1 << 2,

    /// <summary>
    /// Notify about new topics in board
    /// </summary>
    NewTopics = 1 << 3,

    #endregion

    #region Update notifications

    /// <summary>
    /// Notify about status changes (game/blog/character)
    /// </summary>
    StatusChanges = 1 << 4,

    /// <summary>
    /// Notify about character updates
    /// </summary>
    CharacterUpdates = 1 << 5,

    /// <summary>
    /// Notify about new content from subscribed author
    /// </summary>
    AuthorNewContent = 1 << 6,

    #endregion

    #region Notification channels

    /// <summary>
    /// Send in-app notifications
    /// </summary>
    InApp = 1 << 7,

    /// <summary>
    /// Send email notifications
    /// </summary>
    Email = 1 << 8,

    #endregion

    #region Common presets

    /// <summary>
    /// Default settings for game reader
    /// </summary>
    GameReaderDefault = NewPosts | StatusChanges | InApp,

    /// <summary>
    /// Default settings for game player
    /// </summary>
    GamePlayerDefault = NewPosts | StatusChanges | CharacterUpdates | InApp | Email,

    /// <summary>
    /// Default settings for blog reader
    /// </summary>
    BlogReaderDefault = NewPublications | InApp,

    /// <summary>
    /// Default settings for topic subscription
    /// </summary>
    TopicDefault = NewComments | InApp,

    #endregion
}
