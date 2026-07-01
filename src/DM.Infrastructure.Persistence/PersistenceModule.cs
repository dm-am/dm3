using System;
using Autofac;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Availability;
using DM.Domain.Account.Features.Deactivation;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Blacklists;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.Invitations;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Forum.Features.Topics;
using DM.Domain.Game.Features.Games;
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
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Inactivity;
using DM.Domain.Game.Features.Unread;
using DM.Domain.Core.Likes;
using DM.Domain.Core.Notepads;
using DM.Domain.Community.Features.Fundraising;
using DM.Domain.Community.Features.Polls;
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
using DM.Domain.Personal.Features.Blacklists;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Repositories.Account;
using DM.Infrastructure.Persistence.Repositories.Blog;
using DM.Infrastructure.Persistence.Repositories.Community;
using DM.Infrastructure.Persistence.Repositories.Forum;
using DM.Infrastructure.Persistence.Repositories.Messaging;
using DM.Infrastructure.Persistence.Repositories.Moderation;
using DM.Infrastructure.Persistence.Repositories.Game;
using DM.Infrastructure.Persistence.Repositories.Personal;
using DM.Infrastructure.Persistence.Shared.Likes;
using DM.Infrastructure.Persistence.Shared.Notepads;
using DM.Infrastructure.Persistence.Shared.Subscriptions;
using DM.Infrastructure.Persistence.Shared.UnreadCounters;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace DM.Infrastructure.Persistence;

/// <inheritdoc />
public class PersistenceModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        // Configure GUID serialization
        // CSharpLegacy is the default .NET representation, Standard is RFC 9562
        // Using CSharpLegacy for consistency with MongoDB C# driver defaults
        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.CSharpLegacy));
        }
        catch (BsonSerializationException)
        {
            // Serializer already registered, ignore
        }

        // Register default types (validators, factories, etc.) from this assembly
        builder.RegisterDefaultTypes();

        builder.Register(ctx =>
            {
                var connectionString = MongoUrl.Create(ctx.Resolve<IOptions<ConnectionStrings>>().Value.Mongo);
                var settings = MongoClientSettings.FromUrl(connectionString);
                settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
                settings.ConnectTimeout = TimeSpan.FromSeconds(10);
                settings.RetryWrites = true;
                settings.RetryReads = true;
                settings.ClusterConfigurator = cb => cb.Subscribe(
                    new DiagnosticsActivityEventSubscriber(new InstrumentationOptions { CaptureCommandText = true }));
                return new DmMongoClient(settings, connectionString);
            })
            .AsSelf()
            .AsImplementedInterfaces();

        builder.RegisterType<UpdateBuilderFactory>()
            .AsSelf()
            .AsImplementedInterfaces()
            .SingleInstance();

        // Register AutoMapper profiles from this assembly
        builder.RegisterMapper();

        // Community repositories
        builder.RegisterType<PollRepository>()
            .As<IPollRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<UserEndorsementRepository>()
            .As<IUserEndorsementRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<WebsiteTestimonialRepository>()
            .As<IWebsiteTestimonialRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<FundraisingGoalRepository>()
            .As<IFundraisingGoalRepository>()
            .InstancePerLifetimeScope();

        // Forum repositories
        builder.RegisterType<BoardRepository>()
            .As<IBoardRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<BoardModeratorRepository>()
            .As<IBoardModeratorRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<TopicRepository>()
            .As<ITopicRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<TopicCommentRepository>()
            .As<ITopicCommentRepository>()
            .InstancePerLifetimeScope();

        // Messaging repositories
        builder.RegisterType<ChatRepository>()
            .As<IChatRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<MessageRepository>()
            .As<IMessageRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<GlobalChatEventRepository>()
            .As<IGlobalChatEventRepository>()
            .InstancePerLifetimeScope();

        // Moderation repositories
        builder.RegisterType<TicketRepository>()
            .As<ITicketRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<WarningRepository>()
            .As<IWarningRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<BanRepository>()
            .As<IBanRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<ModeratedProfileNoteRepository>()
            .As<IModeratedProfileNoteRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<MentorshipRepository>()
            .As<IMentorshipRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<ModeratedProfileRepository>()
            .As<IModeratedProfileRepository>()
            .InstancePerLifetimeScope();

        // Personal repositories
        builder.RegisterType<NotificationRepository>()
            .As<INotificationRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<UserProfileNoteRepository>()
            .As<IUserProfileNoteRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<UserBlacklistRepository>()
            .As<IUserBlacklistRepository>()
            .As<IUserBlacklistChecker>()
            .InstancePerLifetimeScope();

        builder.RegisterType<BotLinkRepository>()
            .As<IBotLinkRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<UserRepository>()
            .As<IUserRepository>()
            .As<IUserReadRepository>()
            .InstancePerLifetimeScope();

        // Blog repositories
        builder.RegisterType<BlogRepository>()
            .As<IBlogRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<BlogCommentRepository>()
            .As<IBlogCommentRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<PublicationCommentRepository>()
            .As<IPublicationCommentRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<BlogInvitationRepository>()
            .As<IBlogInvitationRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<BlogBlacklistRepository>()
            .As<IBlogBlacklistRepository>()
            .InstancePerLifetimeScope();

        // Account repositories
        builder.RegisterType<EmailLookupRepository>()
            .As<IEmailLookupRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<LoginAttemptRepository>()
            .As<ILoginAttemptRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<AuthenticationRepository>()
            .As<IAuthenticationRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<LoginRecordRepository>()
            .As<ILoginRecordRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<EmailChangeRepository>()
            .As<IEmailChangeRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<EmailChangeConfirmationRepository>()
            .As<IEmailChangeConfirmationRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<PasswordChangeRepository>()
            .As<IPasswordChangeRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<PasswordResetRepository>()
            .As<IPasswordResetRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<RegistrationRepository>()
            .As<IRegistrationRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<ActivationRepository>()
            .As<IActivationRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<TokenVerificationRepository>()
            .As<ITokenVerificationRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<UsernameChangeRepository>()
            .As<IUsernameChangeRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<UsernameHistoryRepository>()
            .As<IUsernameHistoryRepository>()
            .As<DM.Domain.Core.Users.IUsernameHistoryReader>()
            .InstancePerLifetimeScope();

        builder.RegisterType<DeactivationRepository>()
            .As<IDeactivationRepository>()
            .InstancePerLifetimeScope();

        // Game repositories
        builder.RegisterType<GameRepository>()
            .As<IGameRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<GameUserRepository>()
            .As<IGameUserRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<GameBlacklistRepository>()
            .As<IGameBlacklistRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<RoomRepository>()
            .As<IRoomRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<PostRepository>()
            .As<IPostRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<DiceRollRepository>()
            .As<IDiceRollRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<CharacterRepository>()
            .As<ICharacterRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<GameCommentRepository>()
            .As<IGameCommentRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<GameInvitationRepository>()
            .As<IGameInvitationRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<RoomAccessRepository>()
            .As<IRoomAccessRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<PostPendencyRepository>()
            .As<IPostPendencyRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<GameReviewRepository>()
            .As<IGameReviewRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<PostReviewRepository>()
            .As<IPostReviewRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<AttributeSchemaRepository>()
            .As<IAttributeSchemaRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<FirstUnreadRepository>()
            .As<IFirstUnreadRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<InactivityRepository>()
            .As<IInactivityRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<SecurityAuditRepository>()
            .As<ISecurityAuditService>()
            .InstancePerLifetimeScope();

        // Shared repositories
        builder.RegisterType<LikeFactory>()
            .As<ILikeFactory>()
            .InstancePerLifetimeScope();

        builder.RegisterType<LikeRepository>()
            .As<ILikeRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<LikeOperations>()
            .As<ILikeOperations>()
            .InstancePerLifetimeScope();

        builder.RegisterType<NotepadRepository>()
            .As<INotepadRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<SubscriptionRepository>()
            .As<ISubscriptionRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<UnreadCountersRepository>()
            .As<IUnreadCountersRepository>()
            .InstancePerLifetimeScope();

        base.Load(builder);
    }
}
