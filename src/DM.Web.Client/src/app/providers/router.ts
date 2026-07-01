import { createRouter, createWebHistory } from "vue-router";

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
  }
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/",
      name: "home",
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
      meta: { title: "Чат" },
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
          component: () => import("@/pages/messenger/DirectChat.vue"),
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
      path: "/users/:username/:tab(about|games|blogs|topics|achievements)?",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/profile/ProfilePage.vue"),
      },
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
      path: "/forum/:alias",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/forum/ForumPage.vue"),
      },
      children: [
        {
          name: "forum",
          path: "",
          component: () => import("@/pages/forum/TopicsList.vue"),
        },
        {
          path: ":num",
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
        page: () => import("@/features/create-blog/ui/CreateBlogPage.vue"),
      },
    },
    {
      name: "blog",
      path: "/blogs/:id",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/blog/BlogPage.vue"),
      },
    },
    {
      name: "forum-index",
      path: "/forum",
      meta: { title: "Форум" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/forum/ForumIndexPage.vue"),
      },
    },
    {
      path: "/moderation",
      meta: { requiresAuth: true, title: "Модерация" },
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
    {
      name: "create-game",
      path: "/games/create",
      // No requiresAuth: the page renders its own login invitation for guests
      meta: { title: "Новая игра" },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/features/create-game/ui/CreateGamePage.vue"),
      },
    },
    {
      path: "/game/:id",
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
          name: "game-characters",
          path: "characters",
          component: () => import("@/pages/game/GameCharacters.vue"),
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
    // Error page — single dynamic route for /error/:code (400, 401, 403, etc.)
    {
      name: "error",
      path: "/error/:code",
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
  // Query-only changes (sort, filter) keep the scroll position
  if (to.path !== from.path) {
    scrollContentToTop();
  }
  // Set a default document title from the route meta. Dynamic pages override
  // this on the same tick via `useDocumentTitle` after their entity loads, so
  // the composable always wins; static routes keep this title.
  document.title = formatDocumentTitle(to.meta.title);
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
