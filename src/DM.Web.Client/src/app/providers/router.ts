import { createRouter, createWebHistory } from "vue-router";
import { nextTick } from "vue";

import { LeftSidebar, RightSidebar } from "@/widgets/sidebar";
import {
  clearRegistry as clearExpandableRegistry,
  formatDocumentTitle,
} from "@/shared/lib/composables";
import { scrollContentToTop } from "@/shared/lib/scroll";

declare module "vue-router" {
  interface RouteMeta {
    /** Whether the route requires an authenticated user. */
    requiresAuth?: boolean;
    /**
     * Static document title for the route. The brand suffix is appended by the
     * `afterEach` handler. Dynamic pages omit this and call `useDocumentTitle`
     * after their entity loads.
     */
    title?: string;
    /**
     * Marks a route as living inside a game (any /game/:id sub-route). The
     * LeftSidebar keys the per-game GamePanel on this flag.
     */
    gameZone?: boolean;
    /**
     * Marks a route as living inside a blog (any /blogs/:id sub-route). The
     * LeftSidebar keys the per-blog BlogPanel on this flag (mirrors gameZone).
     */
    blogZone?: boolean;
    /**
     * Marks a moderation-zone route (/moderation/*). The LeftSidebar mounts
     * the ModerationPanel navigation on this flag (product doc 4.2.1.5).
     */
    moderationZone?: boolean;
  }
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/",
      name: "home",
      meta: { title: "Главная страница" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/home/HomePage.vue"),
      },
    },
    {
      name: "about",
      path: "/about",
      meta: { title: "О проекте" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/about/AboutPage.vue"),
      },
    },
    {
      name: "testimonials",
      path: "/testimonials",
      meta: { title: "Отзывы о сайте" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/about/TestimonialsPage.vue"),
      },
    },
    {
      name: "polls",
      path: "/polls",
      meta: { title: "Опросы" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/community/PollsPage.vue"),
      },
    },
    {
      name: "rules",
      path: "/rules",
      meta: { title: "Правила" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/rules/RulesPage.vue"),
      },
    },
    {
      name: "global-chat",
      path: "/global-chat",
      meta: { title: "Глобальный чат" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/global-chat/GlobalChatPage.vue"),
      },
    },
    {
      path: "/messenger",
      meta: { requiresAuth: true, title: "Личные сообщения" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/messenger/MessengerPage.vue"),
      },
      children: [
        {
          name: "messenger",
          path: "",
          component: () => import("@/pages/messenger/ChatsList.vue"),
        },
        {
          name: "chat",
          path: "c/:id",
          component: () => import("@/pages/messenger/ChatView.vue"),
        },
        {
          name: "direct-message",
          path: "user/:username",
          component: () => import("@/pages/messenger/DirectChatRedirect.vue"),
        },
      ],
    },
    {
      name: "notifications",
      path: "/notifications",
      meta: { requiresAuth: true, title: "Уведомления" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/personal/NotificationsPage.vue"),
      },
    },
    {
      name: "subscriptions",
      path: "/subscriptions",
      meta: { requiresAuth: true, title: "Подписки" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/personal/SubscriptionsPage.vue"),
      },
    },
    {
      name: "notepad",
      path: "/notepad",
      meta: { requiresAuth: true, title: "Блокнот" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/personal/NotepadPage.vue"),
      },
    },
    {
      name: "my-tickets",
      path: "/my-tickets",
      meta: { requiresAuth: true, title: "Мои обращения" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/personal/MyTicketsPage.vue"),
      },
    },
    {
      name: "site-statistics",
      path: "/statistics",
      meta: { title: "Статистика сайта" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/community/SiteStatisticsPage.vue"),
      },
    },
    {
      name: "community",
      path: "/community",
      meta: { title: "Сообщество" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/community/CommunityPage.vue"),
      },
    },
    {
      name: "profile",
      path: "/users/:username",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/profile/ProfilePage.vue"),
      },
    },
    // Legacy tab-in-URL profile links (/users/X/games etc.). Tabs are pure
    // client state now — old URLs land on the profile with the default tab.
    // Real profile subpages (received-reviews, given-endorsements, ...) are
    // separate routes above/below and never match this regex.
    {
      path: "/users/:username/:tab(about|games|blogs|topics|achievements)",
      redirect: (to) => ({
        name: "profile",
        params: { username: to.params.username },
      }),
    },
    {
      name: "received-reviews",
      path: "/users/:username/received-reviews",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/profile/ReceivedReviewsPage.vue"),
      },
    },
    {
      name: "given-reviews",
      path: "/users/:username/given-reviews",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/profile/GivenReviewsPage.vue"),
      },
    },
    {
      name: "received-endorsements",
      path: "/users/:username/received-endorsements",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/profile/ReceivedEndorsementsPage.vue"),
      },
    },
    {
      name: "given-endorsements",
      path: "/users/:username/given-endorsements",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/profile/GivenEndorsementsPage.vue"),
      },
    },
    {
      name: "profile-uploads",
      path: "/users/:username/uploads",
      // Owner-or-admin gating is done in-page (the router has no role
      // mechanism); the page renders its own access notice otherwise.
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/profile/ProfileUploadsPage.vue"),
      },
    },

    {
      // Single parent record for ALL forum levels (index, board, topic):
      // ForumPage is the persistent shell owning the h1 + board strip, so
      // the strip keeps one live instance across forum navigations and its
      // reorder animation is visible (a per-level remount would reset it).
      path: "/forum",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/forum/ForumPage.vue"),
      },
      children: [
        {
          name: "forum-index",
          path: "",
          meta: { title: "Форум" },
          component: () => import("@/pages/forum/ForumIndexPage.vue"),
        },
        {
          name: "forum",
          path: ":alias",
          component: () => import("@/pages/forum/TopicsList.vue"),
        },
        {
          path: ":alias/:num",
          component: () => import("@/pages/forum/TopicPage.vue"),
          children: [
            {
              name: "topic",
              path: "",
              component: () => import("@/pages/forum/CommentsList.vue"),
            },
          ],
        },
      ],
    },

    {
      name: "games",
      path: "/games",
      meta: { title: "Игры" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/game/GamesPage.vue"),
      },
    },
    {
      name: "blogs",
      path: "/blogs",
      meta: { title: "Блоги" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/blog/BlogsPage.vue"),
      },
    },
    {
      name: "create-blog",
      path: "/blogs/create",
      meta: { requiresAuth: true, title: "Новый блог" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/create-blog/CreateBlogPage.vue"),
      },
    },
    {
      // Blog zone (mirrors the /game/:id section): BlogPage is the shell
      // owning the blog header + store load; sub-pages render in its
      // <router-view>. Paths follow URL_STRUCTURE.md: /blogs/{publicId}/feed
      // is the publication feed, publications nest under it.
      path: "/blogs/:id",
      meta: { blogZone: true },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/blog/BlogPage.vue"),
      },
      children: [
        {
          name: "blog",
          path: "",
          component: () => import("@/pages/blog/BlogDetails.vue"),
        },
        {
          name: "blog-feed",
          path: "feed",
          component: () => import("@/pages/blog/BlogFeed.vue"),
        },
        {
          name: "blog-publication-create",
          path: "feed/create",
          meta: { requiresAuth: true },
          component: () => import("@/pages/blog/PublicationCreate.vue"),
        },
        {
          name: "blog-publication-edit",
          path: "feed/:pubId/edit",
          meta: { requiresAuth: true },
          component: () => import("@/pages/blog/PublicationEdit.vue"),
        },
        {
          name: "blog-comments",
          path: "comments",
          component: () => import("@/pages/blog/BlogComments.vue"),
        },
        {
          name: "blog-settings",
          path: "settings",
          component: () => import("@/pages/blog/BlogSettings.vue"),
        },
        {
          name: "blog-notepad",
          path: "notes",
          component: () => import("@/pages/blog/BlogNotepad.vue"),
        },
      ],
    },
    {
      path: "/moderation",
      meta: { requiresAuth: true, moderationZone: true, title: "Модерация" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/moderation/ModerationPage.vue"),
      },
      children: [
        {
          name: "moderation",
          path: "",
          component: () => import("@/pages/moderation/ModerationOverview.vue"),
        },
        {
          name: "moderation-username-changes",
          path: "username-changes",
          component: () =>
            import("@/pages/moderation/ModerationUsernameChanges.vue"),
        },
        {
          name: "moderation-tags",
          path: "tags",
          component: () => import("@/pages/moderation/ModerationTags.vue"),
        },
        {
          name: "moderation-awards",
          path: "awards",
          component: () => import("@/pages/moderation/ModerationAwards.vue"),
        },
        {
          name: "moderation-awards-series",
          path: "awards/series/:id",
          component: () =>
            import("@/pages/moderation/ModerationAwardsSeries.vue"),
        },
        {
          name: "moderation-award-types",
          path: "award-types",
          component: () =>
            import("@/pages/moderation/ModerationAwardTypes.vue"),
        },
        {
          name: "moderation-achievements",
          path: "achievements",
          component: () =>
            import("@/pages/moderation/ModerationAchievements.vue"),
        },
        {
          name: "moderation-fundraising",
          path: "fundraising",
          component: () => import("@/pages/moderation/FundraisingPage.vue"),
        },
      ],
    },
    // Moderation zone pages (product doc 4.2.1.5, navigated from the
    // left-sidebar ModerationPanel). Top-level records, NOT children of
    // /moderation: each page owns its own H1 and must not render under the
    // admin tab strip of ModerationPage. Role gating is done in-page (the
    // router has no role mechanism); requiresAuth only screens guests.
    {
      name: "moderation-moderators",
      path: "/moderation/moderators",
      meta: { requiresAuth: true, moderationZone: true, title: "Модерация" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/moderation/ModerationModerators.vue"),
      },
    },
    {
      name: "moderation-games",
      path: "/moderation/games",
      meta: {
        requiresAuth: true,
        moderationZone: true,
        title: "Премодерируемые игры",
      },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () =>
          import("@/pages/moderation/ModerationPremoderatedGames.vue"),
      },
    },
    {
      name: "moderation-blogs",
      path: "/moderation/blogs",
      meta: {
        requiresAuth: true,
        moderationZone: true,
        title: "Премодерируемые блоги",
      },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () =>
          import("@/pages/moderation/ModerationPremoderatedBlogs.vue"),
      },
    },
    {
      name: "moderation-bans",
      path: "/moderation/bans",
      meta: {
        requiresAuth: true,
        moderationZone: true,
        title: "Последние баны",
      },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/moderation/ModerationBans.vue"),
      },
    },
    {
      name: "moderation-warnings",
      path: "/moderation/warnings",
      meta: {
        requiresAuth: true,
        moderationZone: true,
        title: "Последние предупреждения",
      },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/moderation/ModerationWarnings.vue"),
      },
    },
    {
      name: "moderation-rated-posts",
      path: "/moderation/rated-posts",
      meta: {
        requiresAuth: true,
        moderationZone: true,
        title: "Последние оцененные посты",
      },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/moderation/ModerationRatedPosts.vue"),
      },
    },
    {
      name: "moderation-new-users",
      path: "/moderation/new-users",
      meta: {
        requiresAuth: true,
        moderationZone: true,
        title: "Новые пользователи",
      },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/moderation/ModerationNewUsers.vue"),
      },
    },
    {
      name: "moderation-violators",
      path: "/moderation/violators",
      meta: { requiresAuth: true, moderationZone: true, title: "Нарушители" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/moderation/ModerationViolators.vue"),
      },
    },
    {
      name: "moderation-support",
      path: "/moderation/support",
      meta: { requiresAuth: true, moderationZone: true, title: "Поддержка" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/moderation/ModerationSupport.vue"),
      },
    },
    {
      name: "moderation-complaints",
      path: "/moderation/complaints",
      meta: { requiresAuth: true, moderationZone: true, title: "Жалобы" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/moderation/ModerationComplaints.vue"),
      },
    },
    {
      name: "moderation-ticket",
      path: "/moderation/tickets/:ticketId",
      meta: { requiresAuth: true, moderationZone: true, title: "Обращение" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/moderation/ModerationTicketPage.vue"),
      },
    },
    {
      name: "moderation-uploads",
      path: "/moderation/uploads",
      meta: {
        requiresAuth: true,
        moderationZone: true,
        title: "Все загруженные файлы",
      },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/moderation/ModerationUploads.vue"),
      },
    },
    {
      name: "create-game",
      path: "/games/create",
      // No requiresAuth: the page renders its own login invitation for guests
      meta: { title: "Новая игра" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/create-game/CreateGamePage.vue"),
      },
    },
    {
      path: "/game/:id",
      meta: { gameZone: true },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/game/GamePage.vue"),
      },
      children: [
        {
          name: "game",
          path: "",
          component: () => import("@/pages/game/GameDetails.vue"),
        },
        {
          name: "game-rooms",
          path: "rooms",
          component: () => import("@/pages/game/GameRooms.vue"),
        },
        {
          name: "game-room",
          path: "rooms/:num",
          component: () => import("@/pages/game/GameRoom.vue"),
        },
        {
          // Chat-type rooms route here (RoomType.Chat) — a message-based,
          // cursor-paginated OOC room, distinct from the post room view.
          name: "game-chat-room",
          path: "chat-rooms/:num",
          component: () => import("@/pages/game/GameChatRoom.vue"),
        },
        {
          name: "game-characters",
          path: "characters",
          component: () => import("@/pages/game/GameCharacters.vue"),
        },
        {
          name: "game-character-create",
          path: "characters/create",
          meta: { requiresAuth: true },
          component: () => import("@/pages/game/CharacterCreate.vue"),
        },
        {
          name: "game-character-edit",
          path: "characters/:characterId/edit",
          meta: { requiresAuth: true },
          component: () => import("@/pages/game/CharacterEdit.vue"),
        },
        {
          name: "game-settings",
          path: "settings",
          component: () => import("@/pages/game/GameSettings.vue"),
        },
        {
          name: "game-comments",
          path: "comments",
          component: () => import("@/pages/game/GameComments.vue"),
        },
        {
          name: "game-reviews",
          path: "reviews",
          component: () => import("@/pages/game/GameReviews.vue"),
        },
        {
          name: "game-post-reviews",
          path: "post-reviews",
          component: () => import("@/pages/game/GamePostReviews.vue"),
        },
        {
          name: "game-notepad",
          path: "notes",
          component: () => import("@/pages/game/GameNotepad.vue"),
        },
      ],
    },
    {
      name: "game-first-unread-post",
      path: "/game/:id/posts/unread",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/game/GameFirstUnreadPost.vue"),
      },
    },
    {
      name: "game-first-unread-comment",
      path: "/game/:id/comments/unread",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/game/GameFirstUnreadComment.vue"),
      },
    },
    // Auth callback for OAuth (Discord, etc.)
    {
      name: "auth-callback",
      path: "/auth/callback",
      components: {
        page: () => import("@/pages/account/AuthCallbackPage.vue"),
      },
    },
    // Session transfer from another mirror
    {
      name: "auth-transfer",
      path: "/auth/transfer",
      meta: { title: "Вход" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/account/TransferPage.vue"),
      },
    },
    {
      name: "account",
      path: "/account",
      meta: { requiresAuth: true, title: "Настройки аккаунта" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/account/AccountPage.vue"),
      },
    },
    {
      name: "activation",
      path: "/activate/:token",
      meta: { title: "Активация аккаунта" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/account/AccountActivationPage.vue"),
      },
    },
    {
      name: "confirm-email",
      path: "/confirm-email/:token",
      meta: { title: "Подтверждение почты" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/account/EmailChangePage.vue"),
      },
    },
    {
      name: "reset-password",
      path: "/reset-password/:token",
      meta: { title: "Сброс пароля" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/account/PasswordResetPage.vue"),
      },
    },
    {
      name: "support",
      path: "/support",
      meta: { title: "Поддержка" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/support/SupportPage.vue"),
      },
    },
    {
      name: "complaint",
      path: "/complaint",
      meta: { title: "Жалоба" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/support/ComplaintPage.vue"),
      },
    },
    {
      name: "support-track",
      path: "/support/track/:token",
      meta: { title: "Статус обращения" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/support/TicketTrackPage.vue"),
      },
    },
    {
      name: "pulse",
      path: "/pulse",
      meta: { title: "Пульс" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/pulse/PulsePage.vue"),
      },
    },
    {
      name: "warnings",
      path: "/warnings",
      meta: { title: "Лог предупреждений" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/warnings/WarningsPage.vue"),
      },
    },
    {
      name: "privacy-policy",
      path: "/privacy",
      meta: { title: "Политика конфиденциальности" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/legal/PrivacyPolicyPage.vue"),
      },
    },
    {
      name: "user-agreement",
      path: "/agreement",
      meta: { title: "Пользовательское соглашение" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/legal/UserAgreementPage.vue"),
      },
    },
    // Forum-topic resolver: deep links that carry only a topic id (e.g.
    // notification "Перейти") land here and are replaced with the canonical
    // /forum/:alias/:num route once the topic is fetched. A dedicated
    // top-level path avoids colliding with /forum/:alias/:num.
    {
      name: "forum-topic-redirect",
      path: "/forum-topic/:topicId",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/redirect/TopicRedirect.vue"),
      },
    },
    // Dev-only mockup catalog (style variants) — removed once the owner
    // picks the variants.
    {
      name: "dev-style-variants",
      path: "/dev/style-variants",
      meta: { title: "Мокапы: стиль" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/dev/StyleVariantsPage.vue"),
      },
    },
    // Error page — dynamic route for /error/:code (400, 401, 403, symbolic
    // OAuth codes…). The code segment is optional so /error?code= also matches.
    {
      name: "error",
      path: "/error/:code?",
      meta: { title: "Ошибка" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/error/ErrorPageRoute.vue"),
      },
    },
    // Catch-all 404 — must be the last route
    {
      name: "not-found",
      path: "/:pathMatch(.*)*",
      meta: { title: "Страница не найдена" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/error/ErrorPageRoute.vue"),
      },
    },
  ],
});

router.beforeEach((to) => {
  // Guests are sent home with the login modal opened instead of a silent redirect
  if (to.meta.requiresAuth && !localStorage.getItem("user")) {
    return { name: "home", query: { action: "login" } };
  }
});

// Wipe the expandable registry when navigating to a DIFFERENT page.
// Query-only changes (sort, filter, pagination) keep the same components
// mounted, so their handles remain valid — clearing would orphan them
// because onMounted won't re-fire.
// Stale dynamic-import (chunk-load) recovery: a new deploy invalidates the
// previous chunk filenames, so an already-open tab fails to lazy-load a route.
// A single full reload pulls the fresh index/chunks. Guarded against reload
// loops via a sessionStorage flag that is cleared once any navigation succeeds.
const CHUNK_RELOAD_FLAG = "dm_chunk_reload";

router.afterEach((to, from) => {
  if (to.name !== from.name) {
    clearExpandableRegistry();
  }
  if (to.hash) {
    // Deep-link to an in-page anchor (e.g. #section-id). Wait for the route
    // component to render (nextTick) and for layout to settle (rAF) before
    // measuring, then scroll the element into view. The scroll container is
    // the custom ".main" div, not the window — scrollIntoView walks up the
    // ancestor chain and handles that natively, unlike window.scrollTo.
    // CommentsList owns its own hash logic (#comment-{id}) with highlight and
    // content-settle delays — only fall back to this generic handler when the
    // target element is actually missing, so it never fights that behavior.
    nextTick(() => {
      requestAnimationFrame(() => {
        const target = document.getElementById(to.hash.slice(1));
        if (target) {
          target.scrollIntoView({ block: "start" });
        } else if (to.path !== from.path) {
          scrollContentToTop();
        }
      });
    });
  } else if (to.path !== from.path) {
    // Query-only changes (sort, filter, pagination) keep the scroll position
    scrollContentToTop();
  }
  // Set a default document title from the route meta — but only when the
  // ROUTE actually changed. Same-route navigations (pagination, filters,
  // param swaps) must not wipe a dynamic title set via `useDocumentTitle`:
  // its watchEffect re-fires on param changes, and static routes' meta
  // title is already in place from the initial navigation.
  if (to.name !== from.name) {
    document.title = formatDocumentTitle(to.meta.title);
  }
  // A navigation completed — the chunks are valid again, so allow a future
  // reload should a later deploy invalidate them.
  sessionStorage.removeItem(CHUNK_RELOAD_FLAG);
});

router.onError((error) => {
  const message = error?.message ?? "";
  const isChunkLoadError =
    /loading (chunk|css chunk|dynamically imported module)/i.test(message) ||
    /import\(\)/i.test(message) ||
    error?.name === "ChunkLoadError";

  if (!isChunkLoadError) return;
  if (sessionStorage.getItem(CHUNK_RELOAD_FLAG)) return;

  sessionStorage.setItem(CHUNK_RELOAD_FLAG, "1");
  window.location.reload();
});

export default router;

export function extractNumberParam(
  param: string | string[],
  defaultValue: number = 1,
) {
  return parseInt(param as string) || defaultValue;
}
