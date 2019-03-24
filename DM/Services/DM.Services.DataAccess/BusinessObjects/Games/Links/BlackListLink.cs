using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Games.Links
{
    [Table("ModuleBlackListLinks")]
    public class BlackListLink
    {
        [Key] public Guid BlackListLinkId { get; set; }
        public Guid GameId { get; set; }
        public Guid UserId { get; set; }

        [ForeignKey(nameof(GameId))] public virtual Game Game { get; set; }

        [ForeignKey(nameof(UserId))] public virtual User User { get; set; }
    }
}