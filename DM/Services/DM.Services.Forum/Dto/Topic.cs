using System;
using System.Collections.Generic;
using DM.Services.Core.Dto;

namespace DM.Services.Forum.Dto
{
    /// <summary>
    /// Topic DTO model
    /// </summary>
    public class Topic
    {
        /// <summary>
        /// Topic identifier
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Forum
        /// </summary>
        public Forum Forum { get; set; }

        /// <summary>
        /// Title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Creation moment
        /// </summary>
        public DateTime CreateDate { get; set; }

        /// <summary>
        /// Author
        /// </summary>
        public GeneralUser Author { get; set; }

        /// <summary>
        /// Total comments count
        /// </summary>
        public int TotalCommentsCount { get; set; }

        /// <summary>
        /// Unread comments count
        /// </summary>
        public int UnreadCommentsCount { get; set; }

        /// <summary>
        /// Last comment
        /// </summary>
        public LastComment LastComment { get; set; }

        /// <summary>
        /// Attached
        /// </summary>
        public bool Attached { get; set; }

        /// <summary>
        /// Closed
        /// </summary>
        public bool Closed { get; set; }

        /// <summary>
        /// Last commentary creation moment or (if none) topic creation moment
        /// </summary>
        public DateTime LastActivityDate { get; set; }

        /// <summary>
        /// Likes
        /// </summary>
        public IEnumerable<GeneralUser> Likes { get; set; }
    }

    /// <summary>
    /// Last commentary DTO model
    /// </summary>
    public class LastComment
    {
        /// <summary>
        /// Author
        /// </summary>
        public GeneralUser Author { get; set; }

        /// <summary>
        /// Creation moment
        /// </summary>
        public DateTime CreateDate { get; set; }
    }
}