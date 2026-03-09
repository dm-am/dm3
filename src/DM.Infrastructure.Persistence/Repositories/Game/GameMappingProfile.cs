using System.Linq;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameTag = DM.Infrastructure.Persistence.Entities.CrossDomain.Tag;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostPendency = DM.Infrastructure.Persistence.Entities.Game.Links.PostPendency;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbComment = DM.Infrastructure.Persistence.Entities.CrossDomain.Comment;
using DtoGameTag = DM.Domain.Game.Features.Games.GameTag;
using DtoRoomAccess = DM.Domain.Game.Features.Games.RoomAccess;
using DtoPostPendency = DM.Domain.Game.Features.Games.PostPendency;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Profile for game DTO and DAL mapping
/// </summary>
internal class GameMappingProfile : Profile
{
    public GameMappingProfile()
    {
        ConfigureRoomMappings();
        ConfigurePostMappings();
        ConfigureCharacterMappings();
        ConfigureCommentMappings();
        ConfigureGameMappings();
    }

    private void ConfigureRoomMappings()
    {
        CreateMap<DbRoom, RoomToUpdate>();

        CreateMap<DbRoom, Room>()
            .Include<DbRoom, RoomToUpdate>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.RoomId))
            .ForMember(d => d.Accesses, s => s.MapFrom(r => r.RoomAccesses))
            .ForMember(d => d.Pendencies, s => s.MapFrom(r => r.PostPendencies
                .Where(p =>
                    p.WaitingForUserId != null &&
                    (
                        p.Room.Game.AuthorId == p.CreatedById ||
                        p.Room.Game.Assistants.Any(a => a.UserId == p.CreatedById) ||
                        p.Room.RoomAccesses.Any(a => a.Character != null && a.Character.AuthorId == p.CreatedById)
                    ) &&
                    (
                        p.Room.Game.AuthorId == p.WaitingForUserId ||
                        p.Room.Game.Assistants.Any(a => a.UserId == p.WaitingForUserId) ||
                        p.Room.RoomAccesses.Any(a => a.Character != null && a.Character.AuthorId == p.WaitingForUserId)
                    ))))
            .ForMember(d => d.TotalPostsCount, s => s.MapFrom(r => r.Posts
                .Count(p => !p.IsRemoved)))
            .ForMember(d => d.Settings, s => s.MapFrom(r => new RoomSettings
            {
                ViewPrivateText = r.ViewPrivateText,
                ViewDiceResults = r.ViewDiceResults,
                DiceEnabled = r.DiceEnabled
            }));

        CreateMap<DbRoom, RoomOrderInfo>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.RoomId));

        CreateMap<DbRoom, RoomNeighbours>()
            .ForMember(d => d.Current, s => s.MapFrom(r => r))
            .ForMember(d => d.Previous, s => s.MapFrom(r => r.PreviousRoom))
            .ForMember(d => d.Next, s => s.MapFrom(r => r.NextRoom));

        CreateMap<DbRoomAccess, DtoRoomAccess>()
            .ForMember(d => d.Id, s => s.MapFrom(l => l.AccessId))
            .ForMember(d => d.TargetType, s => s.MapFrom(l =>
                l.CharacterId.HasValue
                    ? RoomAccessTargetType.Character
                    : RoomAccessTargetType.Reader))
            .ForMember(d => d.User, s => s.MapFrom(l =>
                l.CharacterId.HasValue ? l.Character!.Author : l.ReaderUser));

        CreateMap<DbPostPendency, DtoPostPendency>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.PendencyId))
            .ForMember(d => d.RoomId, s => s.MapFrom(p => p.RoomId))
            .ForMember(d => d.CharacterId, s => s.MapFrom(p => p.CharacterId))
            .ForMember(d => d.CreatedBy, s => s.MapFrom(p => p.CreatedBy))
            .ForMember(d => d.WaitingForUser, s => s.MapFrom(p => p.WaitingForUser))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(p => p.CreatedUtc))
            .ForMember(d => d.FulfilledUtc, s => s.MapFrom(p => p.FulfilledUtc));
    }

    private void ConfigurePostMappings()
    {
        CreateMap<DbPost, Post>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.PostId))
            .ForMember(d => d.Comment, s => s.MapFrom(p => p.Comment));

        CreateMap<DbPost, LastPost>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.PostId))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(p => p.CreatedUtc))
            .ForMember(d => d.RoomId, s => s.MapFrom(p => p.RoomId));
    }

    private void ConfigureCharacterMappings()
    {
        CreateMap<DbCharacter, Character>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CharacterId))
            .ForMember(d => d.PictureUrl, s => s.MapFrom(c => c.Pictures
                .Select(p => p.FilePath)
                .FirstOrDefault()))
            .ForMember(d => d.TotalPostsCount, s => s.MapFrom(c => c.Posts.Count()));

        CreateMap<DbCharacterAttribute, CharacterAttribute>()
            .ForMember(d => d.Id, s => s.MapFrom(a => a.AttributeId));

        CreateMap<DbCharacter, CharacterToUpdate>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CharacterId))
            .ForMember(d => d.GameMasterId, s => s.MapFrom(c => c.Game.AuthorId))
            .ForMember(d => d.GameAssistantIds, s => s.MapFrom(c => c.Game.Assistants.Select(a => a.UserId)));

        CreateMap<DbCharacter, CharacterShort>()
            .Include<DbCharacter, CharacterShortInfo>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CharacterId))
            .ForMember(d => d.PictureUrl, s => s.MapFrom(c => c.Pictures
                .Select(p => p.FilePath)
                .FirstOrDefault()));

        CreateMap<DbCharacter, CharacterShortInfo>()
            .ForMember(d => d.LastPost, s => s.MapFrom(c => c.Posts
                .OrderByDescending(p => p.CreatedUtc)
                .FirstOrDefault()))
            .ForMember(d => d.PostsCount, s => s.MapFrom(c => c.Posts.Count()));
    }

    private void ConfigureCommentMappings()
    {
        // Map DbComment to GameCommentToDelete (for deletion operations)
        // Note: Likes are not needed for delete operations, so we ignore them
        CreateMap<DbComment, GameCommentToDelete>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CommentId))
            .ForMember(d => d.EntityId, s => s.MapFrom(c => c.EntityId))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(c => c.ModifiedUtc))
            .ForMember(d => d.Text, s => s.MapFrom(c => c.Text))
            .ForMember(d => d.Author, s => s.MapFrom(c => c.Author))
            .ForMember(d => d.Likes, s => s.Ignore()) // Not needed for delete operations
            .ForMember(d => d.GameId, s => s.MapFrom(c => c.EntityId))
            .ForMember(d => d.GameCommentCount, s => s.Ignore()) // Set manually
            .ForMember(d => d.IsLastComment, s => s.Ignore()); // Set manually
    }

    private void ConfigureGameMappings()
    {
        CreateMap<DbGame, GameModel>()
            .Include<DbGame, GameExtended>()
            .ForMember(d => d.Id, s => s.MapFrom(g => g.GameId))
            .ForMember(d => d.Tags, s => s.MapFrom(g => g.GameTags.Select(t => t.Tag)))
            .ForMember(d => d.Assistants, s => s.MapFrom(g => g.Assistants.Select(a => a.User)))
            .ForMember(d => d.PendingAssistant, s => s.MapFrom(g => g.Tokens
                .Where(t => t.Type == TokenType.GameAssistantInvitation)
                .Select(t => t.User)
                .FirstOrDefault()))
            .ForMember(d => d.ActiveCharacterUserIds, s => s.MapFrom(g => g.Characters
                .Where(c => c.Status == CharacterStatus.Active && c.AuthorId.HasValue)
                .Select(c => c.AuthorId!.Value)))
            .ForMember(d => d.ReaderUserIds, s => s.Ignore())
            .ForMember(d => d.PendingInvitedUserIds, s => s.MapFrom(g => g.Tokens
                .Where(t => t.Type == TokenType.GamePlayerInvitation || t.Type == TokenType.GameReaderInvitation)
                .Select(t => t.UserId)))
            .ForMember(d => d.PendingPlayerInvitedUserIds, s => s.MapFrom(g => g.Tokens
                .Where(t => t.Type == TokenType.GamePlayerInvitation)
                .Select(t => t.UserId)))
            .ForMember(d => d.BlacklistedUsers, s => s.MapFrom(g => g.BlackList))
            .ForMember(d => d.Recruitment, s => s.MapFrom(g => new GameRecruitment
            {
                IsOpen = g.IsRecruitmentOpen,
                PlayerLimit = g.RecruitmentPlayerLimit,
                PlayerCount = g.Characters
                    .Where(c => c.Status == CharacterStatus.Active && c.AuthorId.HasValue)
                    .Select(c => c.AuthorId!.Value)
                    .Distinct()
                    .Count(),
                StartedUtc = g.RecruitmentStartedUtc
            }));

        CreateMap<GameBlacklist, BlacklistedUser>()
            .ForMember(u => u.UserId, s => s.MapFrom(l => l.BlockedUserId))
            .ForMember(u => u.LinkId, s => s.MapFrom(l => l.EntryId));

        CreateMap<DbGame, GameExtended>()
            .ForMember(d => d.Readers, s => s.Ignore())
            .ForMember(d => d.Characters, s => s.MapFrom(g => g.Characters));

        CreateMap<DbGameTag, DtoGameTag>()
            .ForMember(d => d.Id, s => s.MapFrom(g => g.TagId))
            .ForMember(d => d.GroupTitle, s => s.MapFrom(g => g.TagGroup.Title));
    }
}
