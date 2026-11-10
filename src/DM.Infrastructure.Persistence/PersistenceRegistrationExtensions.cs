using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Availability;
using DM.Domain.Account.Features.Deactivation;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Core.Blacklists;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Popularity;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.Invitations;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Forum.Features.Topics;
using DM.Domain.Forum.Features.Digests;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Popularity;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Blacklists;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Domain.Game.Features.PostPendencies;
using DM.Domain.Game.Features.GameReviews;
using DM.Domain.Game.Features.PostReviews;
using DM.Domain.Game.Features.AttributeSchemaTemplates;
using DM.Domain.Game.Features.GameAttributeSchemas;
using DM.Domain.Game.Features.Inactivity;
using DM.Domain.Game.Features.Unread;
using DM.Domain.Core.Likes;
using DM.Domain.Core.Uploads;
using DM.Domain.Core.Notepads;
using DM.Domain.Community.Features.Fundraising;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Community.Features.Statistics;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Domain.Messaging.Features.Messages;
using DM.Domain.Moderation.Features.Mentorships;
using DM.Domain.Moderation.Features.ProfileNotes;
using DM.Domain.Moderation.Features.Profiles;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Moderation.Features.Warnings;
using DM.Domain.Moderation.Features.Bans;
using DM.Domain.Personal.Features.Blacklists;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Domain.Core.Retention;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Repositories.Account;
using DM.Infrastructure.Persistence.Repositories.Blog;
using DM.Infrastructure.Persistence.Repositories.Community;
using DM.Infrastructure.Persistence.Repositories.Forum;
using DM.Infrastructure.Persistence.Repositories.Messaging;
using DM.Infrastructure.Persistence.Repositories.Moderation;
using DM.Infrastructure.Persistence.Repositories.Game;
using DM.Infrastructure.Persistence.Repositories.General;
using DM.Infrastructure.Persistence.Repositories.Personal;
using DM.Infrastructure.Persistence.Shared.Likes;
using DM.Infrastructure.Persistence.Shared.Notepads;
using DM.Infrastructure.Persistence.Shared.Subscriptions;
using DM.Infrastructure.Persistence.Shared.UnreadCounters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DM.Infrastructure.Persistence;

/// <summary>
/// Services of the persistence module.
/// </summary>
public static class PersistenceRegistrationExtensions
{
    /// <remarks>
    /// The DbContext itself is deliberately absent: each host declares its own
    /// (pooled in the API, plain elsewhere), and the assembly scan refuses
    /// DbContext descendants so this module cannot shadow that choice.
    /// </remarks>
    public static IServiceCollection AddDmPersistence(this IServiceCollection services)
    {
        AddRelationalStorage(services);
        AddCommunity(services);
        AddForum(services);
        AddMessaging(services);
        AddModeration(services);
        AddPersonal(services);
        AddBlog(services);
        AddAccount(services);
        AddGame(services);
        AddShared(services);

        // Default types (validators, factories, etc.) from this assembly
        services.AddDefaultTypes(typeof(PersistenceRegistrationExtensions).Assembly);

        return services;
    }

    private static void AddRelationalStorage(IServiceCollection services)
    {
        // The registry of retention policies lives beside the sweeper.
        services.TryAddScoped<IRetentionSweepProcessor, RetentionSweeper>();

        // Publishing an event is an outbox INSERT on the request's scoped DmDbContext.
        services.TryAddScoped<DM.Domain.Core.Events.IEventProducer, OutboxEventProducer>();

        // The storage half of the relay: claim a batch under FOR UPDATE SKIP
        // LOCKED, run it through the host's publisher, mark the confirmed
        // prefix.
        services.TryAddScoped<DM.Domain.Core.Events.IOutboxRelayProcessor, OutboxRelayProcessor>();
    }

    private static void AddCommunity(IServiceCollection services)
    {
        services.TryAddScoped<IPollRepository, PollRepository>();
        services.TryAddScoped<IUserEndorsementRepository, UserEndorsementRepository>();
        services.TryAddScoped<IWebsiteTestimonialRepository, WebsiteTestimonialRepository>();
        services.TryAddScoped<IFundraisingGoalRepository, FundraisingGoalRepository>();
        services.TryAddScoped<ICommunityStatsRepository, CommunityStatsRepository>();
    }

    private static void AddForum(IServiceCollection services)
    {
        services.TryAddScoped<IBoardRepository, BoardRepository>();
        services.TryAddScoped<IBoardModeratorRepository, BoardModeratorRepository>();
        services.TryAddScoped<ITopicRepository, TopicRepository>();
        services.TryAddScoped<ITopicCommentRepository, TopicCommentRepository>();
        services.TryAddScoped<DM.Domain.Forum.Features.Search.IForumSearchRepository,
            Repositories.Search.ForumSearchRepository>();
        services.TryAddScoped<IPeriodDigestRepository, PeriodDigestRepository>();
    }

    private static void AddMessaging(IServiceCollection services)
    {
        services.TryAddScoped<IChatRepository, ChatRepository>();
        services.TryAddScoped<IMessageRepository, MessageRepository>();
        services.TryAddScoped<IGlobalChatEventRepository, GlobalChatEventRepository>();
        services.TryAddScoped<DM.Domain.Messaging.Features.Search.IMessageSearchRepository,
            Repositories.Search.MessageSearchRepository>();
    }

    private static void AddModeration(IServiceCollection services)
    {
        services.TryAddScoped<ITicketRepository, TicketRepository>();
        services.TryAddScoped<IWarningRepository, WarningRepository>();
        services.TryAddScoped<IWarningEntityResolver, WarningEntityResolver>();
        services.TryAddScoped<IBanRepository, BanRepository>();
        services.TryAddScoped<IModeratedProfileNoteRepository, ModeratedProfileNoteRepository>();
        services.TryAddScoped<IMentorshipRepository, MentorshipRepository>();
        services.TryAddScoped<IModeratedProfileRepository, ModeratedProfileRepository>();
    }

    private static void AddPersonal(IServiceCollection services)
    {
        services.TryAddScoped<INotificationRepository, NotificationRepository>();
        services.TryAddScoped<IUserProfileNoteRepository, UserProfileNoteRepository>();

        // The blacklist repository answers as the checker too, and both reads
        // have to see the writes of the same scope: forwarding factories over
        // one scoped registration, where naive pairs would construct two.
        services.TryAddScoped<UserBlacklistRepository>();
        services.TryAddScoped<IUserBlacklistRepository>(
            provider => provider.GetRequiredService<UserBlacklistRepository>());
        services.TryAddScoped<IUserBlacklistChecker>(
            provider => provider.GetRequiredService<UserBlacklistRepository>());

        services.TryAddScoped<IBotLinkRepository, BotLinkRepository>();

        // Same shared-instance contract for the user repository and its
        // read-side face.
        services.TryAddScoped<UserRepository>();
        services.TryAddScoped<IUserRepository>(
            provider => provider.GetRequiredService<UserRepository>());
        services.TryAddScoped<IUserReadRepository>(
            provider => provider.GetRequiredService<UserRepository>());
    }

    private static void AddBlog(IServiceCollection services)
    {
        services.TryAddScoped<IBlogRepository, BlogRepository>();
        services.TryAddScoped<IPublicationRepository, PublicationRepository>();
        services.TryAddScoped<IBlogPopularityRepository, BlogPopularityRepository>();
        services.TryAddScoped<IBlogCommentRepository, BlogCommentRepository>();
        services.TryAddScoped<IPublicationCommentRepository, PublicationCommentRepository>();
        services.TryAddScoped<IBlogInvitationRepository, BlogInvitationRepository>();
        services.TryAddScoped<IBlogBlacklistRepository, BlogBlacklistRepository>();
    }

    private static void AddAccount(IServiceCollection services)
    {
        services.TryAddScoped<IEmailLookupRepository, EmailLookupRepository>();
        services.TryAddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
        services.TryAddScoped<IAuthenticationRepository, AuthenticationRepository>();
        services.TryAddScoped<ILoginRecordRepository, LoginRecordRepository>();
        services.TryAddScoped<IEmailChangeRepository, EmailChangeRepository>();
        services.TryAddScoped<IEmailChangeConfirmationRepository, EmailChangeConfirmationRepository>();
        services.TryAddScoped<IPasswordChangeRepository, PasswordChangeRepository>();
        services.TryAddScoped<IPasswordResetRepository, PasswordResetRepository>();
        services.TryAddScoped<IRegistrationRepository, RegistrationRepository>();
        services.TryAddScoped<IActivationRepository, ActivationRepository>();
        services.TryAddScoped<IUsernameChangeRepository, UsernameChangeRepository>();

        // The history repository serves the reader face of the same scope.
        services.TryAddScoped<UsernameHistoryRepository>();
        services.TryAddScoped<IUsernameHistoryRepository>(
            provider => provider.GetRequiredService<UsernameHistoryRepository>());
        services.TryAddScoped<DM.Domain.Core.Users.IUsernameHistoryReader>(
            provider => provider.GetRequiredService<UsernameHistoryRepository>());

        services.TryAddScoped<IDeactivationRepository, DeactivationRepository>();

        // The maintenance side of the account tables: read by the background jobs
        // and by nothing that serves a request.
        services.TryAddScoped<ITokenMaintenanceRepository, TokenMaintenanceRepository>();

        services.TryAddScoped<ISecurityAuditRepository, SecurityAuditRepository>();
    }

    private static void AddGame(IServiceCollection services)
    {
        services.TryAddScoped<IGameRepository, GameRepository>();
        services.TryAddScoped<IGamePopularityRepository, GamePopularityRepository>();
        services.TryAddScoped<IGameAssistantRepository, GameAssistantRepository>();
        services.TryAddScoped<IGameBlacklistRepository, GameBlacklistRepository>();
        services.TryAddScoped<IRoomRepository, RoomRepository>();
        services.TryAddScoped<IPostRepository, PostRepository>();
        services.TryAddScoped<IDiceRollRepository, DiceRollRepository>();
        services.TryAddScoped<ICharacterRepository, CharacterRepository>();
        services.TryAddScoped<IGameCommentRepository, GameCommentRepository>();
        services.TryAddScoped<IGameInvitationRepository, GameInvitationRepository>();
        services.TryAddScoped<IRoomAccessRepository, RoomAccessRepository>();
        services.TryAddScoped<IPostPendencyRepository, PostPendencyRepository>();
        services.TryAddScoped<IGameReviewRepository, GameReviewRepository>();
        services.TryAddScoped<IPostReviewRepository, PostReviewRepository>();
        services.TryAddScoped<IAttributeSchemaTemplateRepository, AttributeSchemaTemplateRepository>();
        services.TryAddScoped<IGameAttributeSchemaRepository, GameAttributeSchemaRepository>();
        services.TryAddScoped<IFirstUnreadRepository, FirstUnreadRepository>();
        services.TryAddScoped<IInactivityRepository, InactivityRepository>();
    }

    private static void AddShared(IServiceCollection services)
    {
        services.TryAddScoped<ILikeOperations, LikeOperations>();
        services.TryAddScoped<INotepadRepository, NotepadRepository>();
        services.TryAddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.TryAddScoped<IUnreadCountersRepository, UnreadCountersRepository>();
        services.TryAddScoped<IUploadOrphanCollector, UploadOrphanCollector>();
    }
}
