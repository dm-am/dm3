using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Fora;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Common
{
    [Table("Comments")]
    public class Comment
    {
        [Key]
        public Guid CommentId { get; set; }

        public Guid EntityId { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime? LastUpdateDate { get; set; }

        public string Text { get; set; }

        [ForeignKey(nameof(EntityId))]
        public ForumTopic Topic { get; set; }

        [ForeignKey(nameof(UserId))]
        public User Author { get; set; }

        [InverseProperty(nameof(Like.Comment))]
        public ICollection<Like> Likes { get; set; }
    }
}