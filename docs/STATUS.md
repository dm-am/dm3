Считается скриптом scripts/docs/build-status.mjs; рукой не правится

# Состояние сайта

Маршрутов 96, с кадром 50, ручек 403, без клиента 49, страниц без входа 8, документов 32, с отдельным тестом 10.

Как считается:

- маршрут это именованная запись `router.ts`, перенаправления идут строками без кадра и в итог не входят;
- доступ "вход" значит `requiresAuth` на записи или выше нее; проверки ролей внутри страниц сюда не входят;
- кадр это состояние `scripts/visual/states.ts`, адрес которого открывает маршрут; куда состояние уходит кликами, не учитывается;
- ручки берутся из `artifacts/openapi-routes.json`; у ручки есть клиент, если на нее ведет вызов Api или адрес, который сервер пишет в ответ, а клиент открывает через `apiUrl`;
- тест `test/DM.Architecture.Tests` обходит все документы, если в коде отбирает `.md` по целому дереву, и открывает документ, если в коде называет его путь от корня строковыми литералами; комментарии, сообщения и фикстуры не в счет;
- тест это класс с именем на Should, общий помощник им не считается, а тот, кто зовет обход помощника, обходит все документы сам;
- строка реестра исключений (`new("путь", ...)`, голый литерал в списке, `[InlineData]`) документ не открывает: такой тест идет в колонку "Только в реестре" и в итог "с отдельным тестом" не входит.

## Маршруты

### Главная

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| home | `/` | `home/HomePage.vue` | все | guest-home, guest-login, guest-registration, guest-recovery, guest-two-factor-removal, guest-columns-left, guest-columns-right, member-home, member-logout-menu, member-toast-time-zone, member-tooltip, mentor-home, moderator-home, senior-home, admin-home |

### Сайт и сообщество

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| about | `/about` | `about/AboutPage.vue` | все | guest-about |
| testimonials | `/testimonials` | `about/TestimonialsPage.vue` | все | кадра нет |
| polls | `/polls` | `community/PollsPage.vue` | все | guest-polls |
| rules | `/rules` | `rules/RulesPage.vue` | все | guest-rules |
| site-statistics | `/statistics` | `community/SiteStatisticsPage.vue` | все | guest-statistics, member-statistics-month-picker |
| community | `/community` | `community/CommunityPage.vue` | все | guest-community |
| donate | `/donate` | `donate/DonatePage.vue` | все | кадра нет |
| pulse | `/pulse` | `pulse/PulsePage.vue` | все | member-pulse |
| warnings | `/warnings` | `warnings/WarningsPage.vue` | все | кадра нет |
| privacy-policy | `/privacy` | `legal/PrivacyPolicyPage.vue` | все | кадра нет |
| user-agreement | `/agreement` | `legal/UserAgreementPage.vue` | все | кадра нет |

### Вход и аккаунт

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| account | `/account` | `account/AccountPage.vue` | вход | member-account |
| activation | `/activate` | `account/AccountActivationPage.vue` | все, из письма | letter-activation |
| confirm-email | `/confirm-email` | `account/EmailChangePage.vue` | все, из письма | letter-email-change |
| reset-password | `/reset-password` | `account/PasswordResetPage.vue` | все, из письма | letter-password-reset |
| change-username | `/change-username` | `account/UsernameChangePage.vue` | все, из письма | letter-username-change |
| two-factor-removal | `/two-factor/removal` | `account/TwoFactorRemovalPage.vue` | все, из письма | letter-two-factor-removal |
| two-factor-removal-cancel | `/two-factor/removal/cancel` | `account/TwoFactorRemovalCancelPage.vue` | все, из письма | letter-two-factor-removal-cancel |

### Игры

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| games | `/games` | `game/GamesPage.vue` | все | guest-games, guest-games-filter-root, guest-games-filter-level, guest-games-filter-dates, guest-games-bubbles |
| create-game | `/games/create` | `create-game/CreateGamePage.vue` | все | кадра нет |
| game | `/game/:id` | `game/GameDetails.vue` | все, зона игры | guest-game, member-game-player, member-game-owner, member-columns-left-game, member-columns-right-game, mentor-game |
| game-chat-room | `/game/:id/chat-rooms/:num` | `game/GameChatRoom.vue` | все, зона игры | member-room-chat |
| game-characters | `/game/:id/characters` | `game/GameCharacters.vue` | все, зона игры | member-game-characters |
| game-character-create | `/game/:id/characters/create` | `game/CharacterCreate.vue` | вход, зона игры | кадра нет |
| game-character | `/game/:id/characters/:characterId` | `game/GameCharacter.vue` | все, зона игры | кадра нет |
| game-character-edit | `/game/:id/characters/:characterId/edit` | `game/CharacterEdit.vue` | вход, зона игры | member-character-edit |
| game-settings | `/game/:id/settings` | `game/GameSettings.vue` | все, зона игры | member-game-settings, member-confirm-dialog |
| game-comments | `/game/:id/comments` | `game/GameComments.vue` | все, зона игры | кадра нет |
| game-reviews | `/game/:id/reviews` | `game/GameReviews.vue` | все, зона игры | кадра нет |
| game-post-reviews | `/game/:id/post-reviews` | `game/GamePostReviews.vue` | все, зона игры | кадра нет |
| game-notepad | `/game/:id/notes` | `game/GameNotepad.vue` | все, зона игры | кадра нет |
| game-first-unread-post | `/game/:id/posts/unread` | `game/GameFirstUnreadPost.vue` | все | кадра нет |
| game-first-unread-comment | `/game/:id/comments/unread` | `game/GameFirstUnreadComment.vue` | все | кадра нет |
| game-room | `/game/:id/rooms/:num` | `game/GameRoom.vue` | все, зона игры | guest-room, member-room, member-room-own-post-edit, member-room-big-post, member-room-reviews-open |

### Блоги

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| blogs | `/blogs` | `blog/BlogsPage.vue` | все | guest-blogs |
| create-blog | `/blogs/create` | `create-blog/CreateBlogPage.vue` | все | кадра нет |
| blog | `/blogs/:id` | `blog/BlogDetails.vue` | все, зона блога | member-blog |
| blog-feed | `/blogs/:id/feed` | `blog/BlogFeed.vue` | все, зона блога | member-blog-feed |
| blog-publication-create | `/blogs/:id/feed/create` | `blog/PublicationCreate.vue` | все, зона блога | кадра нет |
| blog-publication | `/blogs/:id/feed/:pubId` | `blog/PublicationPage.vue` | все, зона блога | кадра нет |
| blog-publication-edit | `/blogs/:id/feed/:pubId/edit` | `blog/PublicationEdit.vue` | все, зона блога | кадра нет |
| blog-comments | `/blogs/:id/comments` | `blog/BlogComments.vue` | все, зона блога | кадра нет |
| blog-settings | `/blogs/:id/settings` | `blog/BlogSettings.vue` | все, зона блога | member-blog-settings |
| blog-notepad | `/blogs/:id/notes` | `blog/BlogNotepad.vue` | все, зона блога | кадра нет |

### Форум

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| forum-index | `/forum` | `forum/ForumIndexPage.vue` | все | guest-forum |
| forum | `/forum/:alias` | `forum/TopicsList.vue` | все | guest-forum-board |
| topic | `/forum/:alias/:num` | `forum/CommentsList.vue` | все | guest-forum-topic, member-forum-topic, member-editor-bbcode, member-editor-wysiwyg, member-editor-insert-link, member-editor-insert-image, member-editor-help |
| topic-unread | `/forum/:alias/:num/unread` | `forum/TopicUnread.vue` | все | кадра нет |

### Профиль

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| profile | `/users/:username` | `profile/ProfilePage.vue` | все | guest-profile, member-profile-own, member-profile-own-games, member-profile-other, member-profile-other-achievements, member-profile-removed, moderator-profile, admin-profile-moderation |
| - | `/users/:username/:tab(about\|games\|blogs\|topics\|achievements)` | перенаправление: `profile` | все | - |
| received-reviews | `/users/:username/received-reviews` | `profile/ProfileSubpage.vue` | все | кадра нет |
| given-reviews | `/users/:username/given-reviews` | `profile/ProfileSubpage.vue` | все | member-profile-given-reviews |
| received-endorsements | `/users/:username/received-endorsements` | `profile/ProfileSubpage.vue` | все | кадра нет |
| given-endorsements | `/users/:username/given-endorsements` | `profile/ProfileSubpage.vue` | все | кадра нет |
| received-game-reviews | `/users/:username/received-game-reviews` | `profile/ProfileSubpage.vue` | все | кадра нет |
| given-game-reviews | `/users/:username/given-game-reviews` | `profile/ProfileSubpage.vue` | все | кадра нет |
| write-endorsement | `/users/:username/write-endorsement` | `profile/ProfileEndorsementForm.vue` | вход | кадра нет |
| profile-uploads | `/users/:username/uploads` | `profile/ProfileUploadsPage.vue` | все | кадра нет |

### Личка и чат

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| global-chat | `/global-chat` | `global-chat/GlobalChatPage.vue` | все | member-global-chat-full, member-global-chat-compact |
| messenger | `/messenger` | `messenger/NoChatSelected.vue` | вход | member-messenger |
| chat | `/messenger/c/:id` | `messenger/ChatView.vue` | вход | member-messenger-chat-full, member-messenger-chat-compact |
| direct-message | `/messenger/user/:username` | `messenger/DirectChatRedirect.vue` | вход | кадра нет |

### Уведомления и личное

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| notifications | `/notifications` | `personal/NotificationsPage.vue` | вход | member-notifications |
| subscriptions | `/subscriptions` | `personal/SubscriptionsPage.vue` | вход | member-subscriptions |
| notepad | `/notepad` | `personal/NotepadPage.vue` | вход | member-notepad |

### Модерация

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| - | `/moderation` | перенаправление: `moderation-moderators` | вход, модерация | - |
| moderation-moderators | `/moderation/moderators` | `moderation/ModerationModerators.vue` | вход, модерация | moderator-moderation, admin-moderation |
| moderation-games | `/moderation/games` | `moderation/ModerationPremoderatedGames.vue` | вход, модерация | кадра нет |
| moderation-blogs | `/moderation/blogs` | `moderation/ModerationPremoderatedBlogs.vue` | вход, модерация | кадра нет |
| moderation-bans | `/moderation/bans` | `moderation/ModerationBans.vue` | вход, модерация | moderator-bans |
| moderation-warnings | `/moderation/warnings` | `moderation/ModerationWarnings.vue` | вход, модерация | moderator-warnings |
| moderation-rated-posts | `/moderation/rated-posts` | `moderation/ModerationRatedPosts.vue` | вход, модерация | кадра нет |
| moderation-new-users | `/moderation/new-users` | `moderation/ModerationNewUsers.vue` | вход, модерация | кадра нет |
| moderation-violators | `/moderation/violators` | `moderation/ModerationViolators.vue` | вход, модерация | senior-violators |
| moderation-username-changes | `/moderation/username-changes` | `moderation/ModerationUsernameChanges.vue` | вход, модерация | senior-username-changes |
| moderation-removed-accounts | `/moderation/removed-accounts` | `moderation/ModerationRemovedAccounts.vue` | вход, модерация | admin-removed-accounts |
| moderation-tickets | `/moderation/tickets` | `moderation/ModerationTickets.vue` | вход, модерация | moderator-tickets |
| moderation-ticket | `/moderation/tickets/:ticketId` | `moderation/ModerationTicketPage.vue` | вход, модерация | moderator-ticket |
| moderation-uploads | `/moderation/uploads` | `moderation/ModerationUploads.vue` | вход, модерация | кадра нет |
| moderation-tags | `/moderation/tags` | `moderation/ModerationTags.vue` | вход, модерация | admin-tags |
| moderation-schema-templates | `/moderation/schema-templates` | `moderation/ModerationSchemaTemplates.vue` | вход, модерация | кадра нет |
| moderation-schema-template-new | `/moderation/schema-templates/new` | `moderation/ModerationSchemaTemplate.vue` | вход, модерация | кадра нет |
| moderation-schema-template | `/moderation/schema-templates/:id` | `moderation/ModerationSchemaTemplate.vue` | вход, модерация | кадра нет |
| moderation-awards | `/moderation/awards` | `moderation/ModerationAwards.vue` | вход, модерация | кадра нет |
| moderation-awards-series | `/moderation/awards/series/:id` | `moderation/ModerationAwardsSeries.vue` | вход, модерация | кадра нет |
| moderation-award-types | `/moderation/award-types` | `moderation/ModerationAwardTypes.vue` | вход, модерация | кадра нет |
| moderation-achievements | `/moderation/achievements` | `moderation/ModerationAchievements.vue` | вход, модерация | кадра нет |
| moderation-fundraising | `/moderation/fundraising` | `moderation/FundraisingPage.vue` | вход, модерация | кадра нет |

### Обращения

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| my-tickets | `/my-tickets` | `personal/MyTicketsPage.vue` | вход | member-my-tickets |
| my-ticket | `/my-tickets/:ticketId` | `personal/MyTicketPage.vue` | вход | кадра нет |
| support | `/support` | `support/SupportPage.vue` | все | guest-support |
| complaint | `/complaint` | `support/ComplaintPage.vue` | все | guest-complaint |
| support-track | `/support/track/:token` | `support/TicketTrackPage.vue` | все | guest-ticket-track |

### Служебные

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| - | `/removed-account` | перенаправление: вычисляется (removedAccountRoute) | все | - |
| forum-topic-redirect | `/forum-topic/:topicId` | `redirect/TopicRedirect.vue` | все | кадра нет |
| publication-redirect | `/publication/:pubId` | `redirect/PublicationRedirect.vue` | все | кадра нет |

### Ошибки

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| error | `/error/:code?` | `error/ErrorPageRoute.vue` | все | кадра нет |
| not-found | `/:pathMatch(.*)*` | `error/ErrorPageRoute.vue` | все | guest-not-found |

## Кадры по ролям

| Роль | Состояний |
|---|---|
| guest | 34 |
| user | 44 |
| mentor | 2 |
| moderator | 7 |
| senior | 3 |
| admin | 5 |

## Сервер без клиента

Ручки, на которые не ведет ни один вызов Api клиента и ни один адрес, который сервер пишет в ответ (метод и путь, имена параметров не важны).

- `DELETE /v1/blogs/{id}/users/{userId}`
- `DELETE /v1/boards/{id}/moderators/{username}`
- `DELETE /v1/chat-rooms/{id}`
- `DELETE /v1/games/{id}/characters/unread`
- `DELETE /v1/games/{id}/users/{userId}`
- `DELETE /v1/global-chat/messages/unread`
- `DELETE /v1/moderation/blogs/{blogId}/mentor`
- `DELETE /v1/moderation/games/{gameId}/mentor`
- `DELETE /v1/polls/{id}`
- `DELETE /v1/users/{username}/subscribers`
- `GET /v1/bans`
- `GET /v1/blogs/owner/{login}`
- `GET /v1/chat-rooms/{id}`
- `GET /v1/endorsements/{id}`
- `GET /v1/games/{id}`
- `GET /v1/games/{id}/chat-rooms`
- `GET /v1/games/{id}/readers`
- `GET /v1/games/{id}/users/assistants`
- `GET /v1/global-chat/events/{id}/participants`
- `GET /v1/moderation/notes/{id}`
- `GET /v1/moderation/tags/groups/{groupId}/tags`
- `GET /v1/moderation/tickets/stats`
- `GET /v1/moderation/username-changes/{id}`
- `GET /v1/moderation/users/{username}/notes`
- `GET /v1/polls/{id}`
- `GET /v1/reviews/posts`
- `GET /v1/rooms/{id}`
- `GET /v1/search/forum`
- `GET /v1/testimonials/{id}`
- `GET /v1/topics/{id}/discussion`
- `GET /v1/uploads/{id}`
- `GET /v1/users/me/notes/{username}`
- `GET /v1/users/{username}/bans/active`
- `GET /v1/users/{username}/best-topic`
- `GET /v1/users/{username}/login-history`
- `GET /v1/users/{username}/subscribers`
- `GET /v1/users/{username}/subscribers/me`
- `PATCH /v1/blogs/{id}/rubrics/{rubricId}`
- `PATCH /v1/chat-rooms/{id}`
- `PATCH /v1/moderation/users/{username}/profile`
- `PATCH /v1/moderation/users/{username}/role/{role}`
- `PATCH /v1/rooms/accesses/{id}`
- `POST /v1/boards/{id}/moderators/{username}`
- `POST /v1/games/{id}/chat-rooms`
- `POST /v1/moderation/blogs/{blogId}/mentor`
- `POST /v1/moderation/games/{gameId}/mentor`
- `POST /v1/moderation/tickets`
- `POST /v1/users/{username}/subscribers`
- `PUT /v1/blogs/{id}/rubrics/order`

Ручки, адрес которых сервер пишет в ответ, а клиент открывает через `apiUrl` (`src/DM.Web.Client/src/shared/lib/utils/upload.ts`, `src/DM.Web.Client/src/widgets/game-post/GamePost.vue`, `src/DM.Web.Client/src/widgets/game-post/PostAttachmentRow.vue`), в список выше не входят:

- `GET /v1/uploads/{id}/content`, адрес пишет `src/DM.Web.API/Features/General/Upload/UploadContentRoute.cs`

## Страницы без входа с сайта

Ни имени, ни адреса страницы нет ни в одной ссылке клиента, кроме роутера, спеков и самой страницы.

| Маршрут | Путь | Ведет снаружи |
|---|---|---|
| notepad | `/notepad` | ничто |
| activation | `/activate` | письмо |
| confirm-email | `/confirm-email` | письмо |
| reset-password | `/reset-password` | письмо |
| change-username | `/change-username` | письмо |
| two-factor-removal | `/two-factor/removal` | письмо |
| two-factor-removal-cancel | `/two-factor/removal/cancel` | письмо |
| publication-redirect | `/publication/:pubId` | уведомление |

## Документы

Обходят все документы: DeploymentConfigurationShould, DocumentExampleTypesShould, DocumentLineReferencesShould, DocumentPunctuationShould, DocumentReachabilityShould, DocumentReferencesShould, DocumentSizeShould, DocumentSnapshotsShould, DocumentedCommandsShould, DocumentedPathsShould, DocumentedTablesShould, InterfaceVocabularyShould.

| Документ | Открывают по пути | Только в реестре |
|---|---|---|
| `README.md` | DeploymentConfigurationShould, DocumentReachabilityShould | DocumentPunctuationShould |
| `docs/PROGRESS.md` | отдельного теста нет | DocumentPunctuationShould |
| `docs/STACK.md` | StackDocumentShould | - |
| `docs/STACK_DM2.md` | отдельного теста нет | - |
| `docs/STATUS.md` | StatusDocumentShould | - |
| `docs/SURFACES.md` | отдельного теста нет | DocumentPunctuationShould, DocumentSizeShould, DocumentSnapshotsShould |
| `docs/architecture/AUTHENTICATION.md` | отдельного теста нет | DocumentPunctuationShould |
| `docs/architecture/AUTHORIZATION.md` | отдельного теста нет | DocumentPunctuationShould |
| `docs/architecture/BBCODE_RENDERING.md` | отдельного теста нет | DocumentPunctuationShould, DocumentSizeShould |
| `docs/architecture/SYSTEM.md` | отдельного теста нет | DocumentPunctuationShould |
| `docs/architecture/UPLOADS.md` | отдельного теста нет | DocumentPunctuationShould |
| `docs/conventions/API_DESIGN.md` | RateLimitPipelineShould | DocumentPunctuationShould |
| `docs/conventions/CODE_STYLE.md` | отдельного теста нет | DocumentPunctuationShould, DocumentSnapshotsShould |
| `docs/conventions/DATA_STORAGE.md` | отдельного теста нет | DocumentExampleTypesShould, DocumentPunctuationShould |
| `docs/conventions/PATTERNS.md` | отдельного теста нет | DocumentExampleTypesShould, DocumentPunctuationShould |
| `docs/conventions/PERFORMANCE.md` | отдельного теста нет | DocumentPunctuationShould |
| `docs/conventions/PROCESS.md` | отдельного теста нет | AgentInstructionSizeShould, DocumentPunctuationShould |
| `docs/conventions/SECURITY.md` | IdentifierDisclosureShould | DocumentPunctuationShould |
| `docs/conventions/UI_STANDARDS.md` | отдельного теста нет | DocumentPunctuationShould, DocumentSizeShould, DocumentSnapshotsShould |
| `docs/conventions/URL_STRUCTURE.md` | отдельного теста нет | DocumentPunctuationShould |
| `docs/conventions/USERNAME_POLICY.md` | отдельного теста нет | DocumentPunctuationShould |
| `docs/guides/DEPLOYMENT.md` | DeploymentConfigurationShould, StorageConfigurationShould | DocumentPunctuationShould |
| `docs/guides/LOCAL_SETUP.md` | отдельного теста нет | DocumentPunctuationShould, DocumentedCommandsShould |
| `docs/guides/MONITORING.md` | DeploymentConfigurationShould, LogDeliveryVisibilityShould | DocumentPunctuationShould |
| `docs/guides/POINT_OF_PRESENCE.md` | DeploymentConfigurationShould | DocumentPunctuationShould |
| `docs/guides/TESTING.md` | DocumentedTestTiersShould | DocumentExampleTypesShould, DocumentPunctuationShould |
| `docs/plans/DIALOGS.md` | отдельного теста нет | DocumentLineReferencesShould, DocumentPunctuationShould, DocumentReachabilityShould, DocumentSizeShould, DocumentSnapshotsShould |
| `docs/plans/DM2_MIGRATION.md` | DocumentedTablesShould | DocumentPunctuationShould |
| `docs/plans/ERROR_PAGES_AND_LORE.md` | отдельного теста нет | DocumentPunctuationShould |
| `docs/plans/ROADMAP.md` | отдельного теста нет | DocumentPunctuationShould, DocumentSnapshotsShould |
| `docs/references/CONFIGURATION.md` | отдельного теста нет | DocumentPunctuationShould |
| `docs/references/GLOSSARY.md` | отдельного теста нет | DocumentPunctuationShould |
