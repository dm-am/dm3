import { createRouter, createWebHistory } from "vue-router";

import { GeneralMenu } from "@/widgets/menu";
import { GeneralSidebar } from "@/widgets/sidebar";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/",
      name: "home",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/home/HomePage.vue"),
      },
    },
    {
      name: "about",
      path: "/about/:n?",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/about/AboutPage.vue"),
      },
    },
    {
      path: "/polls",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/community/PollsPage.vue"),
      },
      children: [
        {
          name: "polls",
          path: ":n?",
          component: () => import("@/pages/community/PollsList.vue"),
        },
      ],
    },
    {
      name: "rules",
      path: "/rules",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/rules/RulesPage.vue"),
      },
    },
    {
      name: "globalChat",
      path: "/chat",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/global-chat/GlobalChatPage.vue"),
      },
    },
    {
      path: "/messenger",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/messenger/MessengerPage.vue"),
      },
      children: [
        {
          name: "messenger",
          path: ":n?",
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
      meta: { requiresAuth: true },
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/personal/NotificationsPage.vue"),
      },
    },
    {
      name: "subscriptions",
      path: "/subscriptions",
      meta: { requiresAuth: true },
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/personal/SubscriptionsPage.vue"),
      },
    },
    {
      name: "notepad",
      path: "/notepad",
      meta: { requiresAuth: true },
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/personal/NotepadPage.vue"),
      },
    },
    {
      name: "donate",
      path: "/donate",
      redirect: "/",
    },

    {
      path: "/community",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/community/CommunityPage.vue"),
      },
      children: [
        {
          name: "community",
          path: ":n?",
          meta: { filter: "Active" },
          component: () => import("@/pages/community/UsersList.vue"),
        },
        {
          name: "community-all",
          path: "all/:n?",
          meta: { filter: "All" },
          component: () => import("@/pages/community/UsersList.vue"),
        },
        {
          name: "community-pending",
          path: "pending/:n?",
          meta: { filter: "Pending" },
          component: () => import("@/pages/community/UsersList.vue"),
        },
      ],
    },
    {
      path: "/Azur",
      name: "azur-profile",
      beforeEnter() {
        window.location.href = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        return false;
      },
      redirect: "/",
    },
    {
      name: "profile",
      path: "/users/:username",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/profile/ProfilePage.vue"),
      },
    },
    // Old profile sub-routes redirect to main profile
    {
      path: "/users/:username/games",
      redirect: (to) => ({ name: "profile", params: { username: to.params.username } }),
    },
    {
      path: "/users/:username/characters",
      redirect: (to) => ({ name: "profile", params: { username: to.params.username } }),
    },

    {
      path: "/forum/:id",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/forum/ForumPage.vue"),
      },
      children: [
        {
          name: "forum",
          path: ":n?",
          component: () => import("@/pages/forum/TopicsList.vue"),
        },
      ],
    },
    {
      path: "/topic/:id",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/forum/TopicPage.vue"),
      },
      children: [
        {
          name: "topic",
          path: ":n?",
          component: () => import("@/pages/forum/CommentsList.vue"),
        },
      ],
    },

    {
      path: "/games",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/game/GamesPage.vue"),
      },
      children: [
        {
          name: "games-active",
          path: "",
          meta: { status: "active" },
          component: () => import("@/pages/game/GamesList.vue"),
        },
        {
          name: "games-recruiting",
          path: "recruiting",
          meta: { status: "recruiting" },
          component: () => import("@/pages/game/GamesList.vue"),
        },
        {
          name: "games-finished",
          path: "finished",
          meta: { status: "finished" },
          component: () => import("@/pages/game/GamesList.vue"),
        },
        {
          name: "games-moderation",
          path: "moderation",
          meta: { status: "moderation" },
          component: () => import("@/pages/game/GamesList.vue"),
        },
      ],
    },
    {
      name: "blogs",
      path: "/blogs",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/blog/BlogsPage.vue"),
      },
    },
    {
      name: "forum-index",
      path: "/forum",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/forum/ForumIndexPage.vue"),
      },
    },
    {
      path: "/moderation",
      meta: { requiresAuth: true },
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
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
          component: () => import("@/pages/moderation/ModerationUsernameChanges.vue"),
        },
      ],
    },
    {
      name: "create-game",
      path: "/create-game",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/features/create-game/ui/CreateGamePage.vue"),
      },
    },
    {
      path: "/game/:id",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
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
          path: "rooms/:roomId/:n?",
          component: () => import("@/pages/game/GameRoom.vue"),
        },
        {
          name: "game-characters",
          path: "characters",
          component: () => import("@/pages/game/GameCharacters.vue"),
        },
        {
          name: "game-comments",
          path: "comments/:n?",
          component: () => import("@/pages/game/GameComments.vue"),
        },
      ],
    },
    {
      name: "game-first-unread-post",
      path: "/game/:id/posts/unread",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/game/GameFirstUnreadPost.vue"),
      },
    },
    {
      name: "game-first-unread-comment",
      path: "/game/:id/comments/unread",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
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
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/account/TransferPage.vue"),
      },
    },
    {
      name: "account",
      path: "/account",
      meta: { requiresAuth: true },
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/account/AccountPage.vue"),
      },
    },
    {
      name: "activation",
      path: "/activate/:token",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/account/AccountActivationPage.vue"),
      },
    },
    {
      name: "confirm-email",
      path: "/confirm-email/:token",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/account/EmailChangePage.vue"),
      },
    },
    {
      name: "reset-password",
      path: "/reset-password/:token",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/account/PasswordResetPage.vue"),
      },
    },
    // Dev tools
    {
      name: "theme-colors",
      path: "/dev/theme-colors",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/pages/dev/ThemeColorsPage.vue"),
      },
    },
  ],
});

router.beforeEach((to) => {
  if (to.meta.requiresAuth) {
    // Lazy import to avoid circular dependency
    const userJson = localStorage.getItem("user");
    if (!userJson) {
      return { name: "home" };
    }
  }
});

export default router;

export function extractNumberParam(
  param: string | string[],
  defaultValue: number = 1,
) {
  return parseInt(param as string) || defaultValue;
}
