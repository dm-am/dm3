using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.DataContracts;
using DM.Services.DataAccess.BusinessObjects.Games.Links;

namespace DM.Services.DataAccess.BusinessObjects.Games.Posts
{
    [Table("Rooms")]
    public class Room : IRemovable
    {
        [Key]
        public Guid RoomId { get; set; }

        public Guid GameId { get; set; }

        public string Title { get; set; }
        public RoomAccessType AccessType { get; set; }
        public RoomType Type { get; set; }

        // NOTE: this is the emulation of linked list and needs no navigational properties
        public double OrderNumber { get; set; }
        public Guid? PreviousRoomId { get; set; }
        public Guid? NextRoomId { get; set; }

        public bool IsRemoved { get; set; }

        [ForeignKey(nameof(GameId))]
        public virtual Game Game { get; set; }

        [InverseProperty(nameof(CharacterRoomLink.Room))]
        public virtual ICollection<CharacterRoomLink> CharacterLinks { get; set; }

        [InverseProperty(nameof(Post.Room))]
        public virtual ICollection<Post> Posts { get; set; }

        [InverseProperty(nameof(PostAnticipation.Room))]
        public virtual ICollection<PostAnticipation> PostsAwaited { get; set; }
    }
}