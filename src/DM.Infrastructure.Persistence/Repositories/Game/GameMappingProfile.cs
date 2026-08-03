using System;
using System.Linq;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameTag = DM.Infrastructure.Persistence.Entities.Shared.Tag;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostEdit = DM.Infrastructure.Persistence.Entities.Game.Posts.PostEdit;
using DbPostPendency = DM.Infrastructure.Persistence.Entities.Game.Links.PostPendency;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DtoGameTag = DM.Domain.Game.Features.Games.GameTag;
using DtoRoomAccess = DM.Domain.Game.Features.Games.RoomAccess;
using DtoPostPendency = DM.Domain.Game.Features.Games.PostPendency;
using DtoPostEdit = DM.Domain.Game.Features.Posts.PostEdit;
using GameDto = DM.Domain.Game.Features.Games.Game;

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
        CreateMap<DbRoom, RoomToUpdate>()
            .ForMember(d => d.UnreadPostsCount, opt => opt.Ignore());

        CreateMap<DbRoom, Room>()
            .Include<DbRoom, RoomToUpdate>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.RoomId))
            .ForMember(d => d.RoomNumber, s => s.MapFrom(r => r.RoomNumber))
            .ForMember(d => d.Accesses, s => s.MapFrom(r => r.RoomAccesses))
            .ForMember(d => d.Pendencies, s => s.MapFrom(r => r.PostPendencies
                .Where(p =>
                    p.WaitingForUserId != null &&
                    (
                        p.Room.Game.MasterId == p.CreatedById ||
                        p.Room.Game.Assistants.Any(a => a.UserId == p.CreatedById) ||
                        p.Room.RoomAccesses.Any(a => a.Character != null && a.Character.AuthorId == p.CreatedById)
                    ) &&
                    (
                        p.Room.Game.MasterId == p.WaitingForUserId ||
                        p.Room.Game.Assistants.Any(a => a.UserId == p.WaitingForUserId) ||
                        p.Room.RoomAccesses.Any(a => a.Character != null && a.Character.AuthorId == p.WaitingForUserId)
                    ))))
            .ForMember(d => d.TotalPostsCount, s => s.MapFrom(r => r.Posts
                .Count(p => !p.IsRemoved)))
            .ForMember(d => d.UnreadPostsCount, opt => opt.Ignore())
            .ForMember(d => d.CanView, opt => opt.Ignore()) // Set in repository
            .ForMember(d => d.Description, opt => opt.Ignore()) // Set in repository
            .ForMember(d => d.Settings, s => s.MapFrom(r => new RoomSettings
            {
                ViewPrivateText = r.ViewPrivateText,
                ViewDiceResults = r.ViewDiceResults,
                DiceEnabled = r.DiceEnabled,
                HiddenWithoutAccess = r.HiddenWithoutAccess
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
                l.CharacterId.HasValue ? l.Character!.Author : l.ReaderUser))
            .ForMember(d => d.GrantedUtc, opt => opt.Ignore())
            .ForMember(d => d.GrantedBy, opt => opt.Ignore());

        CreateMap<DbPostPendency, DtoPostPendency>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.PendencyId))
            .ForMember(d => d.RoomId, s => s.MapFrom(p => p.RoomId))
            .ForMember(d => d.CharacterId, s => s.MapFrom(p => p.CharacterId))
            .ForMember(d => d.CharacterName, s => s.MapFrom(p => p.Character.Name))
            .ForMember(d => d.CreatedBy, s => s.MapFrom(p => p.CreatedBy))
            .ForMember(d => d.WaitingForUser, s => s.MapFrom(p => p.WaitingForUser))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(p => p.CreatedUtc))
            .ForMember(d => d.FulfilledUtc, s => s.MapFrom(p => p.FulfilledUtc));
    }

    private void ConfigurePostMappings()
    {
        CreateMap<DbPost, Post>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.PostId))
            .ForMember(d => d.GameText, s => s.MapFrom(p => p.GameText))
            .ForMember(d => d.MetagameText, s => s.MapFrom(p => p.MetagameText))
            .ForMember(d => d.AuthorUserId, s => s.MapFrom(p => p.AuthorId))
            .ForMember(d => d.GameId, s => s.MapFrom(p => p.Room.GameId))
            .ForMember(d => d.RoomId, s => s.MapFrom(p => p.RoomId))
            .ForMember(d => d.SharePrivateWithAll, s => s.MapFrom(p => p.SharePrivateWithAll))
            .ForMember(d => d.RoomViewPrivateText, s => s.MapFrom(p => p.Room.ViewPrivateText))
            // Game leads = master + all assistants (mentors are NOT leads and are
            // intentionally excluded). Projected as two members and joined by the
            // model: concatenating the assistants onto a one-element array inside
            // the projection is what Npgsql refused to translate, and it refused
            // the whole query, so every post read answered 500.
            .ForMember(d => d.GameMasterUserId, s => s.MapFrom(p => p.Room.Game.MasterId))
            .ForMember(d => d.GameAssistantUserIds, s => s.MapFrom(p =>
                p.Room.Game.Assistants.Select(a => a.UserId)))
            // Composed by the model from the two above; a read-only collection is
            // still a destination member as far as configuration validation goes.
            .ForMember(d => d.GameLeadUserIds, opt => opt.Ignore())
            // PrivateAddressee snapshot comes from the JSONB column on the
            // post. ProjectTo carries it over as raw JSON; API-layer
            // mapping profile parses it into a Dictionary at render time.
            .ForMember(d => d.PrivateAddresseeSnapshotJson, s => s.MapFrom(p => p.PrivateAddresseeSnapshotJson))
            .ForMember(d => d.Edits, s => s.MapFrom(p => p.Edits.OrderByDescending(e => e.ModifiedUtc)))
            .ForMember(d => d.Rating, opt => opt.Ignore())
            .ForMember(d => d.ReviewCount, opt => opt.Ignore())
            .ForMember(d => d.AuthorGameRole, opt => opt.Ignore())
            .ForMember(d => d.DiceRolls, opt => opt.Ignore())
            .ForMember(d => d.Room, opt => opt.Ignore());

        CreateMap<DbPostEdit, DtoPostEdit>()
            .ForMember(d => d.Id, s => s.MapFrom(e => e.PostEditId));

        CreateMap<DbPost, LastPost>()
            .ForMember(d => d.Id, s => s.MapFrom(p => p.PostId))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(p => p.CreatedUtc))
            .ForMember(d => d.RoomId, s => s.MapFrom(p => p.RoomId));
    }

    private void ConfigureCharacterMappings()
    {
        CreateMap<DbCharacter, Character>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CharacterId))
            // Picture is loaded by a separate batched query (see PostRepository
            // / CharacterRepository); here we leave the default empty AvatarPicture().
            .ForMember(d => d.Picture, opt => opt.Ignore())
            .ForMember(d => d.ModifiedUtc, opt => opt.Ignore()) // Not stored on the DB entity yet
            .ForMember(d => d.TotalPostsCount, s => s.MapFrom(c => c.Posts.Count()))
            // Aggregate subquery (MAX), not a collection join - split-query safe.
            .ForMember(d => d.LastPostUtc, s => s.MapFrom(c => c.Posts
                .Max(p => (DateTimeOffset?)p.CreatedUtc)))
            // Descriptor is derived from the schema by CharacterAttributeValueFiller
            // in the domain layer, not projectable from the DB row.
            .ForMember(d => d.Descriptor, opt => opt.Ignore());

        CreateMap<DbCharacterAttribute, CharacterAttribute>()
            .ForMember(d => d.Id, s => s.MapFrom(a => a.AttributeId))
            .ForMember(d => d.Title, opt => opt.Ignore())
            .ForMember(d => d.Description, opt => opt.Ignore())
            .ForMember(d => d.Modifier, opt => opt.Ignore())
            // Type is populated by CharacterAttributeValueFiller from the schema,
            // not stored on the attribute row.
            .ForMember(d => d.Type, opt => opt.Ignore())
            .ForMember(d => d.Inconsistent, opt => opt.Ignore());

        CreateMap<DbCharacter, CharacterToUpdate>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CharacterId))
            .ForMember(d => d.GameMasterId, s => s.MapFrom(c => c.Game.MasterId))
            .ForMember(d => d.GameAssistantIds, s => s.MapFrom(c => c.Game.Assistants.Select(a => a.UserId)))
            .ForMember(d => d.IsModified, opt => opt.Ignore());

        CreateMap<DbCharacter, CharacterShort>()
            .Include<DbCharacter, CharacterShortInfo>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CharacterId))
            // Picture is loaded by a separate batched query from Uploads
            // (PostRepository.EnrichWithCharacterPictures).
            .ForMember(d => d.Picture, opt => opt.Ignore());

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
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(c => c.Edits
                .OrderByDescending(e => e.EditedUtc)
                .Select(e => (DateTimeOffset?)e.EditedUtc)
                .FirstOrDefault()))
            .ForMember(d => d.Text, s => s.MapFrom(c => c.Text))
            .ForMember(d => d.Author, s => s.MapFrom(c => c.Author))
            .ForMember(d => d.Likes, s => s.Ignore()) // Not needed for delete operations
            .ForMember(d => d.GameId, s => s.MapFrom(c => c.EntityId))
            .ForMember(d => d.GameCommentCount, s => s.Ignore()) // Set manually
            .ForMember(d => d.IsLastComment, s => s.Ignore()); // Set manually
    }

    private void ConfigureGameMappings()
    {
        // GameAssistant -> GameAssistantInfo (lightweight, for lists and tooltips)
        CreateMap<GameAssistant, GameAssistantInfo>()
            .ForMember(d => d.UserId, s => s.MapFrom(a => a.UserId))
            .ForMember(d => d.Username, s => s.MapFrom(a => a.User.Username))
            .ForMember(d => d.JoinedUtc, s => s.MapFrom(a => a.JoinedUtc))
            .ForMember(d => d.LastActivityUtc, s => s.MapFrom(a => a.User.LastActivityUtc))
            .ForMember(d => d.Role, s => s.MapFrom(a => a.User.Role))
            .ForMember(d => d.IsNewbie, s => s.MapFrom(a => a.User.QuantityRating < 100));

        CreateMap<DbGame, GameDto>()
            .Include<DbGame, GameDetails>()
            .ForMember(d => d.Id, s => s.MapFrom(g => g.GameId))
            .ForMember(d => d.PublicId, s => s.MapFrom(g => g.PublicId))
            .ForMember(d => d.Tags, s => s.MapFrom(g => g.GameTags.Select(t => t.Tag)))
            .ForMember(d => d.TagIds, s => s.MapFrom(g => g.GameTags.Select(t => t.Tag.ShortId)))
            .ForMember(d => d.Assistants, s => s.MapFrom(g => g.Assistants))
            .ForMember(d => d.PendingAssistant, s => s.Ignore()) // Populated via batch query in repository
            .ForMember(d => d.Players, s => s.Ignore()) // Populated in repository for efficiency
            .ForMember(d => d.SubscribersCount, s => s.Ignore()) // Populated via batch query in repository
            .ForMember(d => d.IsViewerSubscriber, s => s.Ignore()) // Populated via batch query in repository
            .ForMember(d => d.PendingInvitedUserIds, s => s.Ignore()) // Populated via batch query in repository
            .ForMember(d => d.PendingPlayerInvitedUserIds, s => s.Ignore()) // Populated via batch query in repository
            .ForMember(d => d.BlacklistedUsers, s => s.MapFrom(g => g.BlackList))
            .ForMember(d => d.Pendencies, opt => opt.Ignore())
            .ForMember(d => d.UnreadPostsCount, opt => opt.Ignore())
            .ForMember(d => d.UnreadCommentsCount, opt => opt.Ignore())
            .ForMember(d => d.UnreadCharactersCount, opt => opt.Ignore())
            .ForMember(d => d.Description, opt => opt.Ignore()) // Alias for NarrativeSetting, set in repository
            .ForMember(d => d.GameReviewsCount, opt => opt.Ignore()) // Set in repository
            .ForMember(d => d.PostReviewsCount, opt => opt.Ignore()) // Set in repository
            .ForMember(d => d.SubscriberUsernames, opt => opt.Ignore()) // Set in repository
            .ForMember(d => d.ActiveCharacters, opt => opt.Ignore()) // Set in repository
            .ForMember(d => d.FilteredPlayerCharacters, opt => opt.Ignore()) // Set in repository (player filter only)
            .ForMember(d => d.Recruitment, s => s.MapFrom(g => new GameRecruitment
            {
                IsOpen = g.IsRecruitmentOpen,
                PcLimit = g.RecruitmentPcLimit,
                PcCount = 0, // Populated via batch query in repository
                StartedUtc = g.RecruitmentStartedUtc,
                IsSubsequent = g.RecruitmentCount >= 2
            }));

        CreateMap<GameBlacklist, BlacklistedUser>()
            .ForMember(u => u.UserId, s => s.MapFrom(l => l.BlockedUserId))
            .ForMember(u => u.LinkId, s => s.MapFrom(l => l.EntryId));

        CreateMap<DbGame, GameDetails>()
            .ForMember(d => d.Subscribers, s => s.Ignore())
            .ForMember(d => d.FullAssistants, s => s.MapFrom(g => g.Assistants.Select(a => a.User)))
            .ForMember(d => d.Characters, s => s.MapFrom(g => g.Characters))
            .ForMember(d => d.AttributeSchema, opt => opt.Ignore())
            .ForMember(d => d.Pendencies, opt => opt.Ignore())
            .ForMember(d => d.UnreadPostsCount, opt => opt.Ignore())
            .ForMember(d => d.UnreadCommentsCount, opt => opt.Ignore())
            // Aggregates computed by GameRepository after the projection
            // (see FillGameDetailStatistics) — they have no entity counterpart.
            .ForMember(d => d.TotalPostsCount, opt => opt.Ignore())
            .ForMember(d => d.LastMasterPostUtc, opt => opt.Ignore())
            .ForMember(d => d.DiceSupported, opt => opt.Ignore());

        CreateMap<DbGameTag, DtoGameTag>()
            .ForMember(d => d.Id, s => s.MapFrom(g => g.TagId))
            .ForMember(d => d.GroupTitle, s => s.MapFrom(g => g.TagGroup.Title))
            .ForMember(d => d.GroupDescription, s => s.MapFrom(g => g.TagGroup.Description))
            .ForMember(d => d.GroupSortOrder, s => s.MapFrom(g => g.TagGroup.SortOrder))
            .ForMember(d => d.Description, s => s.MapFrom(g => g.Description))
            .ForMember(d => d.GamesCount, opt => opt.Ignore()); // Computed at runtime
    }
}
