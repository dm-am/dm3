# DM3 — Текущая структура проекта

> Автогенерировано: 2026-03-09

---

## Проекты (17 C# + 1 Vue)

### Domain Layer (9 проектов)

```
DM.Domain.Core/
├── Abstractions/           # IDateTimeProvider, IGuidFactory, ICorrelationTokenProvider, etc.
├── Authorization/          # IIntentionManager, IIntentionResolver, CommentIntention, UploadIntention
├── Blacklists/             # IUserBlacklistChecker, IContentBlacklistService
├── Caching/                # ICache, CachePolicy
├── Comments/               # ICommentService, Comment, CreateComment, UpdateComment, CommentEntityType
├── Configuration/          # IntegrationSettings, CdnConfiguration, IProbationConfiguration, S3Provider
├── Dto/                    # PagingResult, CursorResult, GeneralUser, Optional, IUser, IReferencesUser
├── Enums/                  # UserRole, GameRole, EventType, TokenType, etc. (~40 enums)
├── Events/                 # IEventProducer, DomainEvent
├── Exceptions/             # HttpException, ValidationError, IntentionManagerException
├── Extensions/             # QueryableExtensions, DescriptionExtensions, FileMimeTypeNames, ReadableGuidHelper
├── Identity/               # IIdentity, IIdentityProvider, IIdentitySetter, Session, UserSettings, AuthenticatedUser
├── Likes/                  # ILikable, ILikeOperations
├── Mail/                   # IMailSender, ITemplateRenderer, IEmailAssetsProvider, EmailLetter, ViewModels/
├── Notepads/               # INotepadRepository, NotepadEntry, NotepadCategory, CreateNotepadEntry
├── Parsing/                # UserAgentParser
├── Reviews/                # Review (shared DTO)
├── Search/                 # ISearchService, ISearchEngineRepository, SearchEntity, FoundEntity
├── Subscriptions/          # ISubscriptionRepository, Subscription, CreateSubscription
├── Tokens/                 # Token, CreateToken
├── UnreadCounters/         # IUnreadCountersRepository, UnreadCountersExtensions
├── Uploads/                # IPublicImageService, IImageProcessingService, Upload, CreateUpload
└── Users/                  # IUserLookupService, IUserReadRepository, IUsernameHistoryReader, UserDetails
```

```
DM.Domain.Account/
├── Authorization/          # AccountIntention, AccountIntentionResolver
├── Configuration/          # AuthenticationConfiguration, CryptoConfiguration
└── Features/
    ├── Authentication/     # IAuthenticationService, IAuthenticationRepository, SessionFactory
    ├── Availability/       # IAvailabilityService, IEmailLookupRepository
    ├── Deactivation/       # IDeactivationService, IDeactivationRepository
    ├── EmailChange/        # IEmailChangeService, IEmailChangeRepository
    ├── Identity/           # IdentityProvider
    ├── PasswordChange/     # IPasswordChangeService, IPasswordChangeRepository
    ├── Recovery/           # IRecoveryService, IPasswordResetRepository
    ├── Registration/       # IRegistrationService, IRegistrationRepository, UserFactory
    ├── Security/           # ISecurityManager, HashProvider, Argon2HashingService
    ├── Tokens/             # ITokenFactory, TokenFactory
    └── UsernameChange/     # IUsernameChangeService, IUsernameChangeRepository
```

```
DM.Domain.Personal/
├── Authorization/          # UserIntention, UserIntentionResolver
└── Features/
    ├── Blacklists/         # IUserBlacklistService, IUserBlacklistRepository
    ├── Notepads/           # IUserNotepadService
    ├── Notifications/      # INotificationService, INotificationRepository
    ├── ProfileNotes/       # IUserProfileNoteService, IUserProfileNoteRepository
    ├── Profiles/           # IUserService, IUserRepository
    └── Subscriptions/      # ISubscriptionService
```

```
DM.Domain.Community/
├── Authorization/          # PollIntention, ReviewIntention, resolvers
└── Features/
    ├── PlatformReviews/    # IPlatformReviewService, IPlatformReviewRepository
    ├── Polls/              # IPollService, IPollRepository, Poll
    ├── Profiles/           # ICommunityProfileService
    └── UserReviews/        # IUserReviewService, IUserReviewRepository
```

```
DM.Domain.Moderation/
├── Authorization/          # ModerationIntention, ModerationIntentionResolver
├── Configuration/          # ProbationConfiguration
└── Features/
    ├── Mentorships/        # IMentorshipService, IMentorshipRepository
    ├── ProfileNotes/       # IModeratedProfileNoteService, IModeratedProfileNoteRepository
    ├── Profiles/           # IModeratedProfileService, IModeratedProfileRepository
    ├── Tickets/            # ITicketService, ITicketRepository, Ticket
    └── Warnings/           # IWarningService, IBanService, IWarningRepository
```

```
DM.Domain.Messaging/
├── Authorization/          # ChatIntention, MessageIntention, GlobalChatEventIntention, resolvers
├── Configuration/          # MessagingConfiguration
└── Features/
    ├── Chats/              # IChatService, IChatRepository, Chat
    ├── GlobalChatEvents/   # IGlobalChatEventService, IGlobalChatEventRepository
    ├── Likes/              # IMessageLikeService
    └── Messages/           # IMessageService, IMessageRepository, Message
```

```
DM.Domain.Game/
├── Authorization/          # GameIntention, CharacterIntention, PostIntention, RoomIntention, resolvers
└── Features/
    ├── AttributeSchemas/   # IAttributeSchemaService, IAttributeSchemaRepository
    ├── Blacklists/         # IGameBlacklistService, IGameBlacklistRepository
    ├── Characters/         # ICharacterService, ICharacterRepository
    ├── Comments/           # IGameCommentService, IGameCommentRepository
    ├── Games/              # IGameService, IGameRepository, GamesQuery
    ├── Invitations/        # IGameInvitationService, IGameInvitationRepository
    ├── Likes/              # IGameCommentLikeService
    ├── Notepads/           # IGameNotepadService
    ├── PostPendencies/     # IPostPendencyService, IPostPendencyRepository
    ├── Posts/              # IPostService, IPostRepository, IFeaturedPostsRepository
    ├── Reviews/            # IGameReviewService, IPostReviewService
    ├── RoomAccesses/       # IRoomAccessService, IRoomAccessRepository
    ├── Rooms/              # IRoomService, IRoomRepository
    ├── Subscriptions/      # IGameSubscriptionService
    └── Unread/             # IFirstUnreadService, IFirstUnreadRepository
```

```
DM.Domain.Blog/
├── Authorization/          # BlogIntention, PublicationIntention, resolvers
└── Features/
    ├── Blacklists/         # IBlogBlacklistService, IBlogBlacklistRepository
    ├── Blogs/              # IBlogService, IBlogRepository, Publication, Rubric
    ├── Comments/           # IBlogCommentService, IBlogCommentRepository
    ├── Invitations/        # IBlogInvitationService, IBlogInvitationRepository
    ├── Likes/              # IBlogLikeService
    ├── Notepads/           # IBlogNotepadService
    ├── PublicationComments/ # IPublicationCommentService, IPublicationCommentRepository
    └── Subscriptions/      # IBlogSubscriptionService
```

```
DM.Domain.Forum/
├── Authorization/          # ForumIntention, TopicIntention, resolvers
└── Features/
    ├── Boards/             # IBoardService, IBoardRepository
    ├── Comments/           # ITopicCommentService, ITopicCommentRepository
    ├── Likes/              # ITopicLikeService
    └── Topics/             # ITopicService, ITopicRepository, Topic
```

### Infrastructure Layer (4 проекта)

```
DM.Infrastructure.Core/
├── Authorization/          # IntentionManager, CommentIntentionResolver
├── Caching/                # MemoryCache
├── Configuration/          # ConnectionStrings, BotConfiguration, IAmazonS3ClientProvider, CloudCubeS3ClientProvider, MinioS3ClientProvider
├── Correlation/            # CorrelationTokenProvider
├── Extensions/             # ModuleRegistrationExtensions, AsyncExtensions, AttributeExtensions
├── Logging/                # LoggingConfiguration, ActivityEnricher
├── Parsing/                # BbParserProvider, BbParserWrapper, TagSetBuilder
├── Search/                 # SearchService, SearchEngineRepository
├── Storage/                # Uploader, PublicImageService, ImageProcessingService, UploadFactory
├── Tracing/                # DmActivitySource
├── CoreModule.cs
├── DateTimeProvider.cs
├── GuidFactory.cs
├── RandomNumberGenerator.cs
├── CursorService.cs
├── SystemUser.cs
└── TypeForwarders.cs
```

```
DM.Infrastructure.Persistence/
├── Design/                 # DesignTimeDbContextFactory
├── Entities/
│   ├── Account/            # DbUser, DbSession, DbToken, Settings/
│   ├── Blog/               # DbBlog, DbPublication, DbRubric
│   ├── CrossDomain/        # DbLike, DbComment, DbUpload
│   ├── DataContracts/      # UserActivityDto, etc.
│   ├── Forum/              # DbBoard, DbTopic
│   ├── Game/               # DbGame, DbRoom, DbPost, Characters/, Links/, Posts/
│   ├── Messaging/          # DbChat, DbMessage, DbGlobalChatEvent
│   ├── Moderation/         # DbWarning, DbBan, DbTicket
│   ├── Notepads/           # DbNotepad, DbNotepadCategory
│   ├── Notifications/      # DbNotification
│   └── Subscriptions/      # DbSubscription
├── Migrations/             # EF Core migrations
├── MongoIntegration/       # MongoDB setup
├── RelationalStorage/      # Shared EF helpers
├── Repositories/
│   ├── Account/            # AuthenticationRepository, LoginAttemptRepository, SecurityAuditRepository
│   ├── Blog/               # BlogRepository, BlogCommentRepository, etc.
│   ├── Community/          # PollRepository, PlatformReviewRepository, UserReviewRepository
│   ├── Forum/              # BoardRepository, TopicRepository, TopicCommentRepository
│   ├── Game/               # GameRepository, RoomRepository, PostRepository
│   ├── General/            # UploadRepository, PublicImageRepository
│   ├── Messaging/          # ChatRepository, MessageRepository, GlobalChatEventRepository
│   ├── Moderation/         # TicketRepository, WarningRepository, BanRepository
│   └── Personal/           # NotificationRepository, UserProfileNoteRepository
├── Shared/
│   ├── Comments/           # CommentRepository
│   ├── Likes/              # LikeOperations
│   ├── Notepads/           # NotepadRepository
│   ├── Subscriptions/      # SubscriptionRepository
│   ├── UnreadCounters/     # UnreadCountersRepository
│   └── Users/              # UserReadRepository, UserLookupService
├── DmDbContext.cs
└── PersistenceModule.cs
```

```
DM.Infrastructure.Mail/
├── Assets/                 # Email templates, logo.png
├── Configuration/          # MailConfiguration
├── Rendering/              # TemplateRenderer, EmailAssetsProvider
├── MailModule.cs
├── MailSender.cs
└── LetterBuilder.cs
```

```
DM.Infrastructure.Messaging/
├── GeneralBus/             # MassTransit setup
├── Outbox/                 # Transactional outbox
├── MessageQueuingModule.cs
├── RabbitMqConfiguration.cs
└── EventProducer.cs (via Outbox)
```

### Web Layer (2 проекта)

```
DM.Web.API/
├── Features/
│   ├── Account/
│   │   ├── Authentication/
│   │   ├── Availability/
│   │   ├── Credentials/
│   │   ├── Deactivation/
│   │   ├── Recovery/
│   │   ├── Registration/
│   │   └── Security/
│   ├── Blog/
│   │   ├── Blacklists/
│   │   ├── Blogs/
│   │   ├── Comments/
│   │   ├── Invitations/
│   │   ├── Likes/
│   │   ├── Notepads/
│   │   ├── PublicationComments/
│   │   ├── Publications/
│   │   ├── Readers/
│   │   └── Users/
│   ├── Community/
│   │   ├── Polls/
│   │   ├── Reviews/
│   │   ├── Statistics/
│   │   └── Users/
│   ├── Forum/
│   │   ├── Boards/
│   │   ├── Comments/
│   │   ├── Likes/
│   │   ├── Moderators/
│   │   └── Topics/
│   ├── Game/
│   │   ├── AttributeSchemas/
│   │   ├── Blacklists/
│   │   ├── Characters/
│   │   ├── Comments/
│   │   ├── Games/
│   │   ├── Invitations/
│   │   ├── Notepads/
│   │   ├── Posts/
│   │   ├── Readers/
│   │   ├── Reviews/
│   │   ├── Rooms/
│   │   ├── Unread/
│   │   └── Users/
│   ├── General/
│   │   ├── Mirror/
│   │   ├── Search/
│   │   └── Upload/
│   ├── Messaging/
│   │   ├── Chats/
│   │   ├── Conversations/
│   │   ├── GlobalChatEvents/
│   │   └── Messages/
│   ├── Moderation/
│   │   ├── Bans/
│   │   ├── Mentorships/
│   │   ├── Profiles/
│   │   ├── Tickets/
│   │   ├── UsernameChanges/
│   │   └── Warnings/
│   └── Personal/
│       ├── Blacklists/
│       ├── Bot/
│       ├── Invitations/
│       ├── Notepads/
│       ├── Notifications/
│       ├── Preferences/
│       ├── ProfileNotes/
│       ├── Profiles/
│       ├── Subscriptions/
│       └── Webhooks/
├── HostedServices/
├── Middleware/
├── Notifications/          # SignalR notifications
├── Realtime/               # SignalR hubs
├── Shared/
│   ├── Authentication/
│   ├── BackgroundServices/
│   ├── BbRendering/
│   ├── Binding/
│   ├── Comments/
│   ├── Configuration/
│   └── Dto/
├── Swagger/
├── Validation/
├── Warmup/
├── Program.cs
└── Startup.cs
```

```
DM.Web.Client/              # Vue 3 + TypeScript (FSD architecture)
├── src/
│   ├── app/
│   │   ├── providers/
│   │   └── styles/
│   ├── pages/
│   │   ├── about/
│   │   ├── account/
│   │   ├── blog/
│   │   ├── community/
│   │   ├── dev/
│   │   ├── forum/
│   │   ├── game/
│   │   ├── global-chat/
│   │   ├── home/
│   │   ├── messenger/
│   │   ├── moderation/
│   │   ├── personal/
│   │   ├── profile/
│   │   └── rules/
│   ├── widgets/
│   │   ├── content-message/
│   │   ├── footer/
│   │   ├── header/
│   │   ├── menu/
│   │   └── sidebar/
│   ├── features/
│   │   ├── auth/
│   │   ├── comment/
│   │   ├── create-game/
│   │   ├── editor/
│   │   ├── topic/
│   │   └── upload/
│   ├── entities/
│   │   ├── blog/
│   │   ├── forum/
│   │   ├── game/
│   │   ├── global-chat/
│   │   ├── message/
│   │   ├── poll/
│   │   └── user/
│   ├── shared/
│   │   ├── api/
│   │   ├── config/
│   │   ├── lib/
│   │   ├── stores/
│   │   └── ui/
│   └── assets/
├── e2e/                    # Playwright tests
├── package.json
└── vite.config.ts
```

### Workers Layer (3 проекта)

```
DM.Workers.Mail/
├── MailSendingConsumer.cs
├── MailSendingProcessor.cs
├── Program.cs
└── Startup.cs
```

```
DM.Workers.NotificationDispatcher/
├── Implementation/
│   ├── Bot/                # Telegram bot notifiers
│   ├── Email/              # Email notifiers
│   └── Notifiers/
│       ├── Blog/
│       ├── Forum/
│       ├── Game/
│       ├── Messaging/
│       ├── Moderation/
│       ├── Security/
│       └── Subscriptions/
├── Program.cs
└── Startup.cs
```

```
DM.Workers.SearchIndexer/
├── Implementation/
│   └── Indexing/
│       └── Indexers/
├── Interceptors/
├── Protos/
├── Program.cs
└── Startup.cs
```

### Test Layer

```
test/
├── DM.Domain.Account.Tests/
├── DM.Domain.Blog.Tests/
├── DM.Domain.Community.Tests/
├── DM.Domain.Forum.Tests/
├── DM.Domain.Game.Tests/
├── DM.Domain.Messaging.Tests/
├── DM.Domain.Moderation.Tests/
├── DM.Domain.Personal.Tests/
├── DM.Infrastructure.Core.Tests/
├── DM.Infrastructure.Messaging.Tests/
├── DM.Infrastructure.Persistence.Tests/
├── DM.Testing/
├── DM.Web.API.IntegrationTests/
└── Directory.Packages.props
```

---

## Зависимости проектов

### Domain Layer

| Проект | Зависит от |
|--------|-----------|
| Domain.Core | — |
| Domain.Account | Domain.Core |
| Domain.Personal | Domain.Core |
| Domain.Community | Domain.Core |
| Domain.Moderation | Domain.Core |
| Domain.Messaging | Domain.Core |
| Domain.Game | Domain.Core |
| Domain.Blog | Domain.Core |
| Domain.Forum | Domain.Core |

### Infrastructure Layer

| Проект | Зависит от |
|--------|-----------|
| Infrastructure.Core | Domain.Core |
| Infrastructure.Persistence | Domain.Core, Domain.*, Infrastructure.Core |
| Infrastructure.Mail | Infrastructure.Core, Infrastructure.Messaging |
| Infrastructure.Messaging | Infrastructure.Core, Infrastructure.Persistence |

---

## Статистика

| Слой | Проектов | Примечание |
|------|----------|-----------|
| Domain | 9 | Бизнес-логика |
| Infrastructure | 4 | Техническая реализация |
| Web.API | 1 | REST API |
| Web.Client | 1 | Vue 3 Frontend (FSD) |
| Workers | 3 | Background processing |
| Tests | 13 | Unit + Integration |
| **Итого** | **31** | 17 C# + 1 Vue + 13 Tests |
