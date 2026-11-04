Считается скриптом scripts/docs/build-status.mjs; рукой не правится

# Состояние сайта

Маршрутов 96, с кадром 60, эндпоинтов 401.

Как считается:

- маршрут это именованная запись `router.ts` и его модулей `routes/`, перенаправления идут строками без кадра и в итог не входят;
- доступ "вход" значит `requiresAuth` на записи или выше нее; проверки ролей внутри страниц сюда не входят;
- кадр это состояние `scripts/visual/states.ts`, адрес которого открывает маршрут; куда состояние уходит кликами, не учитывается;
- эндпоинты берутся из `artifacts/openapi-routes.json`.

## Маршруты

### Главная

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| home | `/` | `home/HomePage.vue` | все | guest-home, guest-login, guest-registration, guest-recovery, guest-two-factor-removal, member-home, member-logout-menu, member-toast-time-zone, member-tooltip, mentor-home, moderator-home, senior-home, admin-home, guest-home-sidebar-failed, guest-home-select-all, guest-home-tab-focus, guest-login-pending-account, guest-login-unknown-email, guest-login-banned, guest-login-empty, guest-registration-empty, guest-registration-weak, guest-registration-bad-email |

### Сайт и сообщество

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| about | `/about` | `about/AboutPage.vue` | все | guest-about |
| testimonials | `/testimonials` | `about/TestimonialsPage.vue` | все | member-testimonials, member-testimonials-wildcard |
| polls | `/polls` | `community/PollsPage.vue` | все | guest-polls, guest-polls-filter, senior-poll-create-refused, senior-poll-edit, senior-poll-cancel-vote |
| rules | `/rules` | `rules/RulesPage.vue` | все | guest-rules, guest-rules-expanded, guest-rules-staff-failed |
| site-statistics | `/statistics` | `community/SiteStatisticsPage.vue` | все | guest-statistics, member-statistics-month-picker, guest-statistics-bad-month |
| community | `/community` | `community/CommunityPage.vue` | все | guest-community, guest-community-tooltip-games, guest-community-tooltip-blogs, member-community, guest-community-filter, guest-community-search-failed |
| donate | `/donate` | `donate/DonatePage.vue` | все | guest-donate |
| pulse | `/pulse` | `pulse/PulsePage.vue` | все | member-pulse, member-pulse-address-params, member-pulse-sort-menu, member-pulse-two-menus |
| warnings | `/warnings` | `warnings/WarningsPage.vue` | все | кадра нет |
| privacy-policy | `/privacy` | `legal/PrivacyPolicyPage.vue` | все | guest-privacy |
| user-agreement | `/agreement` | `legal/UserAgreementPage.vue` | все | guest-agreement |

### Вход и аккаунт

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| account | `/account` | `account/AccountPage.vue` | вход | member-account, member-account-settings-refused, member-account-settings-changed, member-account-password-refused, member-account-password-empty, member-account-sessions-refused, member-account-blacklist-self, member-account-username-short-reason, member-account-invitation-refused |
| activation | `/activate` | `account/AccountActivationPage.vue` | все, из письма | кадра нет |
| confirm-email | `/confirm-email` | `account/EmailChangePage.vue` | все, из письма | кадра нет |
| reset-password | `/reset-password` | `account/PasswordResetPage.vue` | все, из письма | кадра нет |
| change-username | `/change-username` | `account/UsernameChangePage.vue` | все, из письма | кадра нет |
| two-factor-removal | `/two-factor/removal` | `account/TwoFactorRemovalPage.vue` | все, из письма | кадра нет |
| two-factor-removal-cancel | `/two-factor/removal/cancel` | `account/TwoFactorRemovalCancelPage.vue` | все, из письма | кадра нет |

### Игры

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| games | `/games` | `game/GamesPage.vue` | все | guest-games, guest-games-filter-root, guest-games-filter-level, guest-games-filter-dates, guest-games-bubbles, guest-games-filter-status, guest-games-filter-tags, guest-games-bad-tag, guest-game-crazy-adventures |
| create-game | `/games/create` | `create-game/CreateGamePage.vue` | все | member-create-game |
| game | `/game/:id` | `game/GameDetails.vue` | все, зона игры | guest-game, member-game-player, member-game-owner, mentor-game, admin-game-frozen, admin-game-finished |
| game-chat-room | `/game/:id/chat-rooms/:num` | `game/GameChatRoom.vue` | все, зона игры | member-room-chat, member-room-chat-load-failed |
| game-characters | `/game/:id/characters` | `game/GameCharacters.vue` | все, зона игры | member-game-characters, guest-game-characters, member-owned-game-characters |
| game-character-create | `/game/:id/characters/create` | `game/CharacterCreate.vue` | вход, зона игры | кадра нет |
| game-character | `/game/:id/characters/:characterId` | `game/GameCharacter.vue` | все, зона игры | member-game-character, member-game-character-failed, guest-game-character-declined, admin-game-character, member-owned-game-npc |
| game-character-edit | `/game/:id/characters/:characterId/edit` | `game/CharacterEdit.vue` | вход, зона игры | member-character-edit |
| game-settings | `/game/:id/settings` | `game/GameSettings.vue` | все, зона игры | member-game-settings, member-confirm-dialog, admin-game-schema-conflict, admin-game-schema-empty-title, member-game-settings-room-title-long |
| game-comments | `/game/:id/comments` | `game/GameComments.vue` | все, зона игры | кадра нет |
| game-reviews | `/game/:id/reviews` | `game/GameReviews.vue` | все, зона игры | member-game-reviews, guest-game-reviews, guest-game-reviews-garbage |
| game-post-reviews | `/game/:id/post-reviews` | `game/GamePostReviews.vue` | все, зона игры | кадра нет |
| game-notepad | `/game/:id/notes` | `game/GameNotepad.vue` | все, зона игры | кадра нет |
| game-first-unread-post | `/game/:id/posts/unread` | `game/GameFirstUnreadPost.vue` | все | кадра нет |
| game-first-unread-comment | `/game/:id/comments/unread` | `game/GameFirstUnreadComment.vue` | все | кадра нет |
| game-room | `/game/:id/rooms/:num` | `game/GameRoom.vue` | все, зона игры | guest-room, member-room, member-room-own-post-edit, member-room-big-post, member-room-reviews-open, member-room-address-params, member-room-sort-menu, admin-room-pendency-hover, member-room-composer-roll, member-room-composer-attachment, member-room-composer-attachment-selected, member-room-composer-attachment-chosen, guest-room-hidden-file, member-room-marked-file |

### Блоги

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| blogs | `/blogs` | `blog/BlogsPage.vue` | все | guest-blogs |
| create-blog | `/blogs/create` | `create-blog/CreateBlogPage.vue` | все | кадра нет |
| blog | `/blogs/:id` | `blog/BlogDetails.vue` | все, зона блога | member-blog, admin-blog-active, admin-blog-closed, guest-blog-rubric-hover, member-blog-other |
| blog-feed | `/blogs/:id/feed` | `blog/BlogFeed.vue` | все, зона блога | member-blog-feed |
| blog-publication-create | `/blogs/:id/feed/create` | `blog/PublicationCreate.vue` | все, зона блога | кадра нет |
| blog-publication | `/blogs/:id/feed/:pubId` | `blog/PublicationPage.vue` | все, зона блога | кадра нет |
| blog-publication-edit | `/blogs/:id/feed/:pubId/edit` | `blog/PublicationEdit.vue` | все, зона блога | кадра нет |
| blog-comments | `/blogs/:id/comments` | `blog/BlogComments.vue` | все, зона блога | кадра нет |
| blog-settings | `/blogs/:id/settings` | `blog/BlogSettings.vue` | все, зона блога | member-blog-settings, member-blog-settings-invitations |
| blog-notepad | `/blogs/:id/notes` | `blog/BlogNotepad.vue` | все, зона блога | кадра нет |

### Форум

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| forum-index | `/forum` | `forum/ForumIndexPage.vue` | все | guest-forum, member-forum-index |
| forum | `/forum/:alias` | `forum/TopicsList.vue` | все | guest-forum-board, moderator-forum-board, guest-forum-news, guest-forum-news-topic |
| topic | `/forum/:alias/:num` | `forum/CommentsList.vue` | все | guest-forum-topic, member-forum-topic, member-editor-bbcode, member-editor-wysiwyg, member-editor-insert-link, member-editor-insert-image, member-editor-help, member-forum-comments-wildcard, member-forum-like-tooltip, member-forum-comment-refused, moderator-comment-warn-dialog, moderator-editor-toolbar, member-editor-private-warning, member-editor-link-bad-address, member-editor-image-size-refused |
| topic-unread | `/forum/:alias/:num/unread` | `forum/TopicUnread.vue` | все | кадра нет |

### Профиль

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| profile | `/users/:username` | `profile/ProfilePage.vue` | все | guest-profile, member-profile-own, member-profile-own-games, member-profile-other, member-profile-other-achievements, member-profile-removed, moderator-profile, admin-profile-moderation, member-profile-rating, guest-profile-offline, member-profile-assistants-focus, member-profile-own-lowercase, member-profile-games-player, member-profile-games-autoswitch, member-profile-other-topics, member-profile-achievement-popover, member-profile-achievements-blocks, mentor-profile-subscribe-menu, member-profile-subscribe-failed, admin-profile-own-edit, admin-warn-dialog, admin-ban-dialog, admin-ban-repeat-refused |
| - | `/users/:username/:tab(about\|games\|blogs\|topics\|achievements)` | перенаправление: `profile` | все | - |
| received-reviews | `/users/:username/received-reviews` | `profile/ProfileSubpage.vue` | все | кадра нет |
| given-reviews | `/users/:username/given-reviews` | `profile/ProfileSubpage.vue` | все | member-profile-given-reviews |
| received-endorsements | `/users/:username/received-endorsements` | `profile/ProfileSubpage.vue` | все | guest-profile-received-endorsements, admin-endorsements-edit-fresh, admin-endorsements-edit-late |
| given-endorsements | `/users/:username/given-endorsements` | `profile/ProfileSubpage.vue` | все | кадра нет |
| received-game-reviews | `/users/:username/received-game-reviews` | `profile/ProfileSubpage.vue` | все | кадра нет |
| given-game-reviews | `/users/:username/given-game-reviews` | `profile/ProfileSubpage.vue` | все | кадра нет |
| write-endorsement | `/users/:username/write-endorsement` | `profile/ProfileEndorsementForm.vue` | вход | member-write-endorsement |
| profile-uploads | `/users/:username/uploads` | `profile/ProfileUploadsPage.vue` | все | admin-profile-own-uploads |

### Личка и чат

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| global-chat | `/global-chat` | `global-chat/GlobalChatPage.vue` | все | member-global-chat-full, member-global-chat-compact, member-global-chat-like-tooltip, member-global-chat-search-empty |
| messenger | `/messenger` | `messenger/NoChatSelected.vue` | вход | member-messenger, member-messenger-search |
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
| moderation-bans | `/moderation/bans` | `moderation/ModerationBans.vue` | все, модерация | moderator-bans, admin-unban-dialog |
| moderation-warnings | `/moderation/warnings` | `moderation/ModerationWarnings.vue` | вход, модерация | moderator-warnings, admin-points-dialog |
| moderation-rated-posts | `/moderation/rated-posts` | `moderation/ModerationRatedPosts.vue` | вход, модерация | moderator-rated-posts |
| moderation-new-users | `/moderation/new-users` | `moderation/ModerationNewUsers.vue` | вход, модерация | кадра нет |
| moderation-violators | `/moderation/violators` | `moderation/ModerationViolators.vue` | вход, модерация | senior-violators |
| moderation-username-changes | `/moderation/username-changes` | `moderation/ModerationUsernameChanges.vue` | вход, модерация | senior-username-changes |
| moderation-removed-accounts | `/moderation/removed-accounts` | `moderation/ModerationRemovedAccounts.vue` | вход, модерация | admin-removed-accounts, admin-removed-accounts-search-empty |
| moderation-tickets | `/moderation/tickets` | `moderation/ModerationTickets.vue` | вход, модерация | moderator-tickets, moderator-tickets-subtype |
| moderation-ticket | `/moderation/tickets/:ticketId` | `moderation/ModerationTicketPage.vue` | вход, модерация | moderator-ticket |
| moderation-uploads | `/moderation/uploads` | `moderation/ModerationUploads.vue` | вход, модерация | кадра нет |
| moderation-tags | `/moderation/tags` | `moderation/ModerationTags.vue` | вход, модерация | admin-tags, admin-tag-dialog |
| moderation-schema-templates | `/moderation/schema-templates` | `moderation/ModerationSchemaTemplates.vue` | вход, модерация | кадра нет |
| moderation-schema-template-new | `/moderation/schema-templates/new` | `moderation/ModerationSchemaTemplate.vue` | вход, модерация | кадра нет |
| moderation-schema-template | `/moderation/schema-templates/:id` | `moderation/ModerationSchemaTemplate.vue` | вход, модерация | кадра нет |
| moderation-awards | `/moderation/awards` | `moderation/ModerationAwards.vue` | вход, модерация | admin-award-series-create-year |
| moderation-awards-series | `/moderation/awards/series/:id` | `moderation/ModerationAwardsSeries.vue` | вход, модерация | admin-award-series-refused |
| moderation-award-types | `/moderation/award-types` | `moderation/ModerationAwardTypes.vue` | вход, модерация | admin-award-types-refused, admin-award-type-dialog |
| moderation-achievements | `/moderation/achievements` | `moderation/ModerationAchievements.vue` | вход, модерация | admin-achievements, admin-achievement-category-dialog, admin-achievement-tier-dialog |
| moderation-fundraising | `/moderation/fundraising` | `moderation/FundraisingPage.vue` | вход, модерация | admin-fundraising |

### Обращения

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| my-tickets | `/my-tickets` | `personal/MyTicketsPage.vue` | вход | member-my-tickets |
| my-ticket | `/my-tickets/:ticketId` | `personal/MyTicketPage.vue` | вход | кадра нет |
| support | `/support` | `support/SupportPage.vue` | все | guest-support, guest-support-sent, guest-support-editor-typing, guest-support-editor-draft |
| complaint | `/complaint` | `support/ComplaintPage.vue` | все | guest-complaint |
| support-track | `/support/track` | `support/TicketTrackPage.vue` | все | guest-ticket-track, guest-ticket-track-old-link |
| - | `/support/track/:token` | перенаправление: `support-track` | все | - |

### Служебные

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| forum-topic-redirect | `/forum-topic/:topicId` | `redirect/TopicRedirect.vue` | все | кадра нет |
| publication-redirect | `/publication/:pubId` | `redirect/PublicationRedirect.vue` | все | кадра нет |
| - | `/removed-account` | перенаправление: вычисляется (removedAccountRoute) | все | - |

### Ошибки

| Маршрут | Путь | Страница | Доступ | Кадры |
|---|---|---|---|---|
| error | `/error/:code?` | `error/ErrorPageRoute.vue` | все | кадра нет |
| not-found | `/:pathMatch(.*)*` | `error/ErrorPageRoute.vue` | все | guest-not-found, letter-activation, letter-email-change, letter-password-reset, letter-username-change, letter-two-factor-removal, letter-two-factor-removal-cancel |

## Кадры по ролям

| Роль | Состояний |
|---|---|
| guest | 71 |
| user | 93 |
| mentor | 3 |
| moderator | 12 |
| senior | 6 |
| admin | 32 |

Состояний, которые скрипт не прочел: 2.
