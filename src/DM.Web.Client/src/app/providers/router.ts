import {
  createRouter,
  createWebHistory,
  RouterView,
  type RouteLocationNormalized,
  type RouteLocationRaw,
} from "vue-router";
import { nextTick } from "vue";

import { LeftSidebar, RightSidebar } from "@/widgets/sidebar";
import { removedAccountRoute, restoreSession } from "@/entities/user";
import { trackHash } from "@/entities/ticket";
import { clearRegistry as clearExpandableRegistry } from "@/shared/lib/composables/useExpandableRegistry";
import { formatDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { scrollContentToTop } from "@/shared/lib/scroll";
import { loginLocation } from "@/shared/lib/auth";
import { useAuthStore } from "@/shared/stores";
import { accountRoutes } from "./routes/account";
import { blogRoutes } from "./routes/blog";
import { forumRoutes } from "./routes/forum";
import { gameRoutes } from "./routes/game";
import { moderationRoutes } from "./routes/moderation";
import { profileRoutes } from "./routes/profile";
import "./routeMeta";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // One record owns the shell, so a new page cannot forget the sidebars; a
    // page without them is a sibling of this record.
    {
      path: "/",
      components: { left: LeftSidebar, right: RightSidebar, page: RouterView },
      children: [
        {
          path: "",
          name: "home",
          // The same as the static <title> of index.html, or the tab renames
          // itself on load.
          meta: { title: "Форумные ролевые игры" },
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
          name: "my-ticket",
          path: "/my-tickets/:ticketId",
          meta: { requiresAuth: true, dynamicTitle: true },
          component: () => import("@/pages/personal/MyTicketPage.vue"),
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
        ...profileRoutes,
        ...forumRoutes,
        ...gameRoutes,
        ...blogRoutes,
        ...moderationRoutes,
        ...accountRoutes,
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
          path: "/support/track",
          meta: { title: "Статус обращения" },
          component: () => import("@/pages/support/TicketTrackPage.vue"),
        },
        // Old links carry the token in the path; it is in the logs already.
        {
          path: "/support/track/:token",
          redirect: (to) => ({
            name: "support-track",
            hash: trackHash(String(to.params.token)),
          }),
        },
        {
          name: "donate",
          path: "/donate",
          meta: { title: "Помочь проекту" },
          component: () => import("@/pages/donate/DonatePage.vue"),
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
        // Old links of the "Аккаунт удален" signature.
        {
          path: "/removed-account",
          redirect: removedAccountRoute,
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
        // The code segment is optional so /error?code= also matches.
        {
          name: "error",
          path: "/error/:code?",
          // The page names the error from the code (getErrorConfig).
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
    // The messenger lays its list of conversations where the right column stands.
    {
      path: "/messenger",
      components: { left: LeftSidebar, page: RouterView },
      meta: { requiresAuth: true },
      children: [
        {
          path: "",
          component: () => import("@/pages/messenger/MessengerPage.vue"),
          children: [
            {
              name: "messenger",
              path: "",
              meta: { title: "Личные сообщения" },
              component: () => import("@/pages/messenger/NoChatSelected.vue"),
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
      ],
    },
    // A room of posts has no right column, and a child cannot drop a column its
    // parent mounts, so it is a sibling of the shell. Each level keeps the
    // component of the other tree at the same depth, so the game shell stays
    // mounted on the way in and out of a room.
    {
      path: "/game/:id/rooms/:num",
      components: { left: LeftSidebar, page: RouterView },
      children: [
        {
          path: "",
          meta: { gameZone: true },
          component: () => import("@/pages/game/GamePage.vue"),
          children: [
            {
              name: "game-room",
              path: "",
              meta: { dynamicTitle: true },
              component: () => import("@/pages/game/GameRoom.vue"),
            },
          ],
        },
      ],
    },
  ],
});

/**
 * Sends a guest to the login window with the address he asked for. Reads the
 * store, not the persisted copy, after restoreSession: the cookie may carry a
 * session the browser has no stored copy of.
 */
export async function guardAuthenticated(
  to: RouteLocationNormalized,
): Promise<RouteLocationRaw | undefined> {
  if (!to.meta.requiresAuth) return undefined;
  if (!useAuthStore().isAuthenticated) await restoreSession();
  if (!useAuthStore().isAuthenticated) return loginLocation(to.fullPath);
  return undefined;
}

router.beforeEach(guardAuthenticated);

// A new deploy renames the chunks, so an open tab reloads once; the flag stops
// a reload loop and is cleared by any navigation that succeeds.
const CHUNK_RELOAD_FLAG = "dm_chunk_reload";

router.afterEach((to, from) => {
  // Query-only changes keep the components mounted, and their handles valid.
  if (to.name !== from.name) {
    clearExpandableRegistry();
  }
  if (to.hash) {
    // After render and layout; scrollIntoView, since the page scrolls in ".main".
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
  // Same-route navigations keep a dynamic title set by useDocumentTitle.
  if (to.name !== from.name) {
    document.title = formatDocumentTitle(to.meta.title);
  }
  sessionStorage.removeItem(CHUNK_RELOAD_FLAG);
});

router.onError((error) => {
  const message = error?.message ?? "";
  const isChunkLoadError =
    /loading (chunk|css chunk|dynamically imported module)/i.test(message) ||
    /import\(\)/i.test(message) ||
    error?.name === "ChunkLoadError";

  if (!isChunkLoadError) {
    // vue-router stops logging errors itself once a listener is registered.
    console.error("Navigation error:", error);
    return;
  }
  if (sessionStorage.getItem(CHUNK_RELOAD_FLAG)) return;

  sessionStorage.setItem(CHUNK_RELOAD_FLAG, "1");
  window.location.reload();
});

export default router;
