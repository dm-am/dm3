import { createRouter, createWebHistory } from "vue-router";
import { nextTick } from "vue";

import { LeftSidebar, RightSidebar } from "@/widgets/sidebar";
import ShellPage from "./ShellPage.vue";
import {
  clearRegistry as clearExpandableRegistry,
  formatDocumentTitle,
} from "@/shared/lib/composables";
import { scrollContentToTop } from "@/shared/lib/scroll";
import { useAuthStore } from "@/shared/stores";

// Tab-title contract: every named route declares exactly one of `title`,
// `section` and `dynamicTitle`, and declares it in its OWN meta. A title
// inherited from a parent record is one tab name shared by every child — that
// is how eight moderation pages and three messenger pages became
// indistinguishable. `router.spec.ts` holds both halves of the rule.
declare module "vue-router" {
  interface RouteMeta {
    /** Whether the route requires an authenticated user. */
    requiresAuth?: boolean;
    /**
     * Static document title for the route. The brand suffix is appended by the
     * `afterEach` handler.
     */
    title?: string;
    /**
     * Section of a route inside a zone (game, blog). The zone shell composes
     * "{сущность} | {секция}" once the entity loads, entity first: it is what
     * tells two tabs apart when the browser truncates the title.
     */
    section?: string;
    /**
     * The title is composed at runtime by the route's component chain — the
     * page itself or the zone shell above it — out of data that exists only
     * after a fetch (the interlocutor, the room, the error code).
     */
    dynamicTitle?: true;
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
    // One record owns the shell. Every page below is a child of it, so a new
    // route cannot render without the sidebars by forgetting to declare them —
    // which is exactly how the OAuth callback below ended up bare. Opting out
    // is now structural: be a sibling of this record, not a child.
    {
      path: "/",
      components: { left: LeftSidebar, right: RightSidebar, page: ShellPage },
      children: [
        {
          path: "",
          name: "home",
          meta: { title: "Главная страница" },
          component: () => import("@/pages/home/HomePage.vue"),
        },
        {
          name: "about",
          path: "/about",
          meta: { title: "О проекте" },
          component: () => import("@/pages/about/AboutPage.vue"),
        },
        {
          name: "testimonials",
          path: "/testimonials",
          meta: { title: "Отзывы о сайте" },
          component: () => import("@/pages/about/TestimonialsPage.vue"),
        },
        {
          name: "polls",
          path: "/polls",
          meta: { title: "Опросы" },
          component: () => import("@/pages/community/PollsPage.vue"),
        },
        {
          name: "rules",
          path: "/rules",
          meta: { title: "Правила" },
          component: () => import("@/pages/rules/RulesPage.vue"),
        },
        {
          name: "global-chat",
          path: "/global-chat",
          meta: { title: "Глобальный чат" },
          component: () => import("@/pages/global-chat/GlobalChatPage.vue"),
        },
        {
          path: "/messenger",
          meta: { requiresAuth: true },
          component: () => import("@/pages/messenger/MessengerPage.vue"),
          children: [
            {
              name: "messenger",
              path: "",
              meta: { title: "Личные сообщения" },
              component: () => import("@/pages/messenger/ChatsList.vue"),
            },
            {
              name: "chat",
              path: "c/:id",
              meta: { dynamicTitle: true },
              component: () => import("@/pages/messenger/ChatView.vue"),
            },
            {
              name: "direct-message",
              path: "user/:username",
              meta: { title: "Переход к переписке" },
              component: () =>
                import("@/pages/messenger/DirectChatRedirect.vue"),
            },
          ],
        },
        {
          name: "notifications",
          path: "/notifications",
          meta: { requiresAuth: true, title: "Уведомления" },
          component: () => import("@/pages/personal/NotificationsPage.vue"),
        },
        {
          name: "subscriptions",
          path: "/subscriptions",
          meta: { requiresAuth: true, title: "Подписки" },
          component: () => import("@/pages/personal/SubscriptionsPage.vue"),
        },
        {
          name: "notepad",
          path: "/notepad",
          meta: { requiresAuth: true, title: "Блокнот" },
          component: () => import("@/pages/personal/NotepadPage.vue"),
        },
        {
          name: "my-tickets",
          path: "/my-tickets",
          meta: { requiresAuth: true, title: "Мои обращения" },
          component: () => import("@/pages/personal/MyTicketsPage.vue"),
        },
        {
          name: "site-statistics",
          path: "/statistics",
          meta: { title: "Статистика сайта" },
          component: () => import("@/pages/community/SiteStatisticsPage.vue"),
        },
        {
          name: "community",
          path: "/community",
          meta: { title: "Сообщество" },
          component: () => import("@/pages/community/CommunityPage.vue"),
        },
        {
          name: "profile",
          path: "/users/:username",
          meta: { dynamicTitle: true },
          component: () => import("@/pages/profile/ProfilePage.vue"),
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
          meta: { dynamicTitle: true },
          component: () => import("@/pages/profile/ReceivedReviewsPage.vue"),
        },
        {
          name: "given-reviews",
          path: "/users/:username/given-reviews",
          meta: { dynamicTitle: true },
          component: () => import("@/pages/profile/GivenReviewsPage.vue"),
        },
        {
          name: "received-endorsements",
          path: "/users/:username/received-endorsements",
          meta: { dynamicTitle: true },
          component: () =>
            import("@/pages/profile/ReceivedEndorsementsPage.vue"),
        },
        {
          name: "given-endorsements",
          path: "/users/:username/given-endorsements",
          meta: { dynamicTitle: true },
          component: () => import("@/pages/profile/GivenEndorsementsPage.vue"),
        },
        {
          name: "profile-uploads",
          path: "/users/:username/uploads",
          meta: { dynamicTitle: true },
          // Owner-or-admin gating is done in-page (the router has no role
          // mechanism); the page renders its own access notice otherwise.
          component: () => import("@/pages/profile/ProfileUploadsPage.vue"),
        },

        {
          // Single parent record for ALL forum levels (index, board, topic):
          // ForumPage is the persistent shell owning the h1 + board strip, so
          // the strip keeps one live instance across forum navigations and its
          // reorder animation is visible (a per-level remount would reset it).
          path: "/forum",
          component: () => import("@/pages/forum/ForumPage.vue"),
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
              meta: { dynamicTitle: true },
              component: () => import("@/pages/forum/TopicsList.vue"),
            },
            {
              path: ":alias/:num",
              component: () => import("@/pages/forum/TopicPage.vue"),
              children: [
                {
                  name: "topic",
                  path: "",
                  meta: { dynamicTitle: true },
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
          component: () => import("@/pages/game/GamesPage.vue"),
        },
        {
          name: "blogs",
          path: "/blogs",
          meta: { title: "Блоги" },
          component: () => import("@/pages/blog/BlogsPage.vue"),
        },
        {
          name: "create-blog",
          path: "/blogs/create",
          meta: { requiresAuth: true, title: "Новый блог" },
          component: () => import("@/pages/create-blog/CreateBlogPage.vue"),
        },
        {
          // Blog zone (mirrors the /game/:id section): BlogPage is the shell
          // owning the blog header + store load; sub-pages render in its
          // <router-view>. Paths follow URL_STRUCTURE.md: /blogs/{publicId}/feed
          // is the publication feed, publications nest under it.
          //
          // The shell owns the tab title for the whole zone: the blog name,
          // then `section` of the active sub-route. The blog root adds no
          // section, which is why it declares `dynamicTitle` instead.
          path: "/blogs/:id",
          meta: { blogZone: true },
          component: () => import("@/pages/blog/BlogPage.vue"),
          children: [
            {
              name: "blog",
              path: "",
              meta: { dynamicTitle: true },
              component: () => import("@/pages/blog/BlogDetails.vue"),
            },
            {
              name: "blog-feed",
              path: "feed",
              meta: { section: "Лента публикаций" },
              component: () => import("@/pages/blog/BlogFeed.vue"),
            },
            {
              name: "blog-publication-create",
              path: "feed/create",
              meta: { requiresAuth: true, section: "Создание публикации" },
              component: () => import("@/pages/blog/PublicationCreate.vue"),
            },
            {
              name: "blog-publication-edit",
              path: "feed/:pubId/edit",
              meta: {
                requiresAuth: true,
                section: "Редактирование публикации",
              },
              component: () => import("@/pages/blog/PublicationEdit.vue"),
            },
            {
              name: "blog-comments",
              path: "comments",
              meta: { section: "Обсуждение" },
              component: () => import("@/pages/blog/BlogComments.vue"),
            },
            {
              name: "blog-settings",
              path: "settings",
              meta: { section: "Настройки блога" },
              component: () => import("@/pages/blog/BlogSettings.vue"),
            },
            {
              name: "blog-notepad",
              path: "notes",
              meta: { section: "Заметки блога" },
              component: () => import("@/pages/blog/BlogNotepad.vue"),
            },
          ],
        },
        {
          path: "/moderation",
          meta: { requiresAuth: true, moderationZone: true },
          component: () => import("@/pages/moderation/ModerationPage.vue"),
          children: [
            {
              name: "moderation",
              path: "",
              meta: { title: "Модерация" },
              component: () =>
                import("@/pages/moderation/ModerationOverview.vue"),
            },
            {
              name: "moderation-username-changes",
              path: "username-changes",
              meta: { title: "Запросы на смену имени пользователя" },
              component: () =>
                import("@/pages/moderation/ModerationUsernameChanges.vue"),
            },
            {
              name: "moderation-tags",
              path: "tags",
              meta: { title: "Теги игр" },
              component: () => import("@/pages/moderation/ModerationTags.vue"),
            },
            {
              name: "moderation-awards",
              path: "awards",
              meta: { title: "Награды" },
              component: () =>
                import("@/pages/moderation/ModerationAwards.vue"),
            },
            {
              name: "moderation-awards-series",
              path: "awards/series/:id",
              meta: { title: "Серия конкурсов" },
              component: () =>
                import("@/pages/moderation/ModerationAwardsSeries.vue"),
            },
            {
              name: "moderation-award-types",
              path: "award-types",
              meta: { title: "Каталог типов наград" },
              component: () =>
                import("@/pages/moderation/ModerationAwardTypes.vue"),
            },
            {
              name: "moderation-achievements",
              path: "achievements",
              meta: { title: "Достижения" },
              component: () =>
                import("@/pages/moderation/ModerationAchievements.vue"),
            },
            {
              name: "moderation-fundraising",
              path: "fundraising",
              meta: { title: "Сбор средств" },
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
          meta: {
            requiresAuth: true,
            moderationZone: true,
            title: "Модерация",
          },
          component: () =>
            import("@/pages/moderation/ModerationModerators.vue"),
        },
        {
          name: "moderation-games",
          path: "/moderation/games",
          meta: {
            requiresAuth: true,
            moderationZone: true,
            title: "Премодерируемые игры",
          },
          component: () =>
            import("@/pages/moderation/ModerationPremoderatedGames.vue"),
        },
        {
          name: "moderation-blogs",
          path: "/moderation/blogs",
          meta: {
            requiresAuth: true,
            moderationZone: true,
            title: "Премодерируемые блоги",
          },
          component: () =>
            import("@/pages/moderation/ModerationPremoderatedBlogs.vue"),
        },
        {
          name: "moderation-bans",
          path: "/moderation/bans",
          meta: {
            requiresAuth: true,
            moderationZone: true,
            title: "Последние баны",
          },
          component: () => import("@/pages/moderation/ModerationBans.vue"),
        },
        {
          name: "moderation-warnings",
          path: "/moderation/warnings",
          meta: {
            requiresAuth: true,
            moderationZone: true,
            title: "Последние предупреждения",
          },
          component: () => import("@/pages/moderation/ModerationWarnings.vue"),
        },
        {
          name: "moderation-rated-posts",
          path: "/moderation/rated-posts",
          meta: {
            requiresAuth: true,
            moderationZone: true,
            title: "Последние оцененные посты",
          },
          component: () =>
            import("@/pages/moderation/ModerationRatedPosts.vue"),
        },
        {
          name: "moderation-new-users",
          path: "/moderation/new-users",
          meta: {
            requiresAuth: true,
            moderationZone: true,
            title: "Новые пользователи",
          },
          component: () => import("@/pages/moderation/ModerationNewUsers.vue"),
        },
        {
          name: "moderation-violators",
          path: "/moderation/violators",
          meta: {
            requiresAuth: true,
            moderationZone: true,
            title: "Нарушители",
          },
          component: () => import("@/pages/moderation/ModerationViolators.vue"),
        },
        {
          name: "moderation-support",
          path: "/moderation/support",
          meta: {
            requiresAuth: true,
            moderationZone: true,
            title: "Поддержка",
          },
          component: () => import("@/pages/moderation/ModerationSupport.vue"),
        },
        {
          name: "moderation-complaints",
          path: "/moderation/complaints",
          meta: { requiresAuth: true, moderationZone: true, title: "Жалобы" },
          component: () =>
            import("@/pages/moderation/ModerationComplaints.vue"),
        },
        {
          name: "moderation-ticket",
          path: "/moderation/tickets/:ticketId",
          meta: {
            requiresAuth: true,
            moderationZone: true,
            dynamicTitle: true,
          },
          component: () =>
            import("@/pages/moderation/ModerationTicketPage.vue"),
        },
        {
          name: "moderation-uploads",
          path: "/moderation/uploads",
          meta: {
            requiresAuth: true,
            moderationZone: true,
            title: "Все загруженные файлы",
          },
          component: () => import("@/pages/moderation/ModerationUploads.vue"),
        },
        {
          name: "create-game",
          path: "/games/create",
          // No requiresAuth: the page renders its own login invitation for guests
          meta: { title: "Новая игра" },
          component: () => import("@/pages/create-game/CreateGamePage.vue"),
        },
        {
          // GamePage (the zone shell) owns the tab title for every route
          // below: the game name first, then `section` of the active
          // sub-route. A route whose section is data cannot be spelled by
          // the shell: the two rooms name their room, the character form
          // names its mode, so they declare `dynamicTitle` and compose the
          // title themselves.
          path: "/game/:id",
          meta: { gameZone: true },
          component: () => import("@/pages/game/GamePage.vue"),
          children: [
            {
              name: "game",
              path: "",
              meta: { dynamicTitle: true },
              component: () => import("@/pages/game/GameDetails.vue"),
            },
            {
              name: "game-room",
              path: "rooms/:num",
              meta: { dynamicTitle: true },
              component: () => import("@/pages/game/GameRoom.vue"),
            },
            {
              // Chat-type rooms route here (RoomType.Chat) — a message-based,
              // cursor-paginated OOC room, distinct from the post room view.
              name: "game-chat-room",
              path: "chat-rooms/:num",
              meta: { dynamicTitle: true },
              component: () => import("@/pages/game/GameChatRoom.vue"),
            },
            {
              name: "game-characters",
              path: "characters",
              meta: { section: "Персонажи" },
              component: () => import("@/pages/game/GameCharacters.vue"),
            },
            {
              name: "game-character-create",
              path: "characters/create",
              // The section is the ?npc flag: "Новый NPC" or "Новый
              // персонаж". Meta cannot spell a section that is data, so the
              // page composes the title, and its H1 reads the same string.
              meta: { requiresAuth: true, dynamicTitle: true },
              component: () => import("@/pages/game/CharacterCreate.vue"),
            },
            {
              name: "game-character-edit",
              path: "characters/:characterId/edit",
              meta: { requiresAuth: true, section: "Редактирование персонажа" },
              component: () => import("@/pages/game/CharacterEdit.vue"),
            },
            {
              name: "game-settings",
              path: "settings",
              meta: { section: "Настройки" },
              component: () => import("@/pages/game/GameSettings.vue"),
            },
            {
              name: "game-comments",
              path: "comments",
              meta: { section: "Обсуждение" },
              component: () => import("@/pages/game/GameComments.vue"),
            },
            {
              name: "game-reviews",
              path: "reviews",
              meta: { section: "Рецензии" },
              component: () => import("@/pages/game/GameReviews.vue"),
            },
            {
              name: "game-post-reviews",
              path: "post-reviews",
              meta: { section: "Оцененные посты" },
              component: () => import("@/pages/game/GamePostReviews.vue"),
            },
            {
              name: "game-notepad",
              path: "notes",
              meta: { section: "Блокнот мастера" },
              component: () => import("@/pages/game/GameNotepad.vue"),
            },
          ],
        },
        {
          name: "game-first-unread-post",
          path: "/game/:id/posts/unread",
          // Outside the game shell (a top-level record), so the zone title does
          // not reach it: a resolver page with a title of its own.
          meta: { title: "Переход к непрочитанному посту" },
          component: () => import("@/pages/game/GameFirstUnreadPost.vue"),
        },
        {
          name: "game-first-unread-comment",
          path: "/game/:id/comments/unread",
          meta: { title: "Переход к непрочитанному комментарию" },
          component: () => import("@/pages/game/GameFirstUnreadComment.vue"),
        },
        {
          name: "account",
          path: "/account",
          meta: { requiresAuth: true, title: "Настройки аккаунта" },
          component: () => import("@/pages/account/AccountPage.vue"),
        },
        {
          name: "activation",
          path: "/activate/:token",
          meta: { title: "Активация аккаунта" },
          component: () => import("@/pages/account/AccountActivationPage.vue"),
        },
        {
          name: "confirm-email",
          path: "/confirm-email/:token",
          meta: { title: "Подтверждение почты" },
          component: () => import("@/pages/account/EmailChangePage.vue"),
        },
        {
          name: "reset-password",
          path: "/reset-password/:token",
          meta: { title: "Сброс пароля" },
          component: () => import("@/pages/account/PasswordResetPage.vue"),
        },
        {
          name: "support",
          path: "/support",
          meta: { title: "Поддержка" },
          component: () => import("@/pages/support/SupportPage.vue"),
        },
        {
          name: "complaint",
          path: "/complaint",
          meta: { title: "Жалоба" },
          component: () => import("@/pages/support/ComplaintPage.vue"),
        },
        {
          name: "support-track",
          path: "/support/track/:token",
          meta: { title: "Статус обращения" },
          component: () => import("@/pages/support/TicketTrackPage.vue"),
        },
        {
          name: "pulse",
          path: "/pulse",
          meta: { title: "Пульс" },
          component: () => import("@/pages/pulse/PulsePage.vue"),
        },
        {
          name: "warnings",
          path: "/warnings",
          meta: { title: "Лог предупреждений" },
          component: () => import("@/pages/warnings/WarningsPage.vue"),
        },
        {
          name: "privacy-policy",
          path: "/privacy",
          meta: { title: "Политика конфиденциальности" },
          component: () => import("@/pages/legal/PrivacyPolicyPage.vue"),
        },
        {
          name: "user-agreement",
          path: "/agreement",
          meta: { title: "Пользовательское соглашение" },
          component: () => import("@/pages/legal/UserAgreementPage.vue"),
        },
        // Forum-topic resolver: deep links that carry only a topic id (e.g.
        // notification "Перейти") land here and are replaced with the canonical
        // /forum/:alias/:num route once the topic is fetched. A dedicated
        // top-level path avoids colliding with /forum/:alias/:num.
        {
          name: "forum-topic-redirect",
          path: "/forum-topic/:topicId",
          meta: { title: "Переход к теме" },
          component: () => import("@/pages/redirect/TopicRedirect.vue"),
        },
        // Mockup catalogs under /dev are registered in development builds only. Vite
        // substitutes import.meta.env.DEV with false when building, so rollup drops the
        // branch together with the dynamic import and the chunk is never emitted. A
        // comment promising future removal is not a mechanism; this is.
        ...(import.meta.env.DEV
          ? [
              {
                name: "dev-style-variants",
                path: "/dev/style-variants",
                meta: { title: "Мокапы: стиль" },
                component: () => import("@/pages/dev/StyleVariantsPage.vue"),
              },
            ]
          : []),
        // Error page — dynamic route for /error/:code (400, 401, 403, symbolic
        // OAuth codes…). The code segment is optional so /error?code= also matches.
        {
          name: "error",
          path: "/error/:code?",
          // The page names the error from the code in the URL (getErrorConfig);
          // a static title here would be a second source going stale.
          meta: { dynamicTitle: true },
          component: () => import("@/pages/error/ErrorPageRoute.vue"),
        },
        // Catch-all 404 — must be the last route
        {
          name: "not-found",
          path: "/:pathMatch(.*)*",
          meta: { dynamicTitle: true },
          component: () => import("@/pages/error/ErrorPageRoute.vue"),
        },
      ],
    },
    // Outside the shell on purpose: the OAuth provider redirects here, the page
    // only exchanges the code and navigates on, and sidebars would flash for a
    // frame with no session to render from.
    {
      name: "auth-callback",
      path: "/auth/callback",
      meta: { title: "Внешняя авторизация" },
      component: () => import("@/pages/account/AuthCallbackPage.vue"),
    },
  ],
});

router.beforeEach((to) => {
  // Guests are sent home with the login modal opened instead of a silent
  // redirect. Read the store, not the persisted copy: the two answered
  // differently between a 401 and the next reload, so the guard bounced a
  // viewer the rest of the interface was still drawing as signed in.
  if (to.meta.requiresAuth && !useAuthStore().isAuthenticated) {
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
