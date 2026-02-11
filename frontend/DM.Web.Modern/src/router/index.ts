import { createRouter, createWebHistory } from "vue-router";

import GeneralMenu from "@/views/layout/GeneralMenu.vue";
import GeneralSidebar from "@/views/layout/GeneralSidebar.vue";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/",
      name: "home",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/home/HomePage.vue"),
      },
    },
    {
      name: "about",
      path: "/about/:n?",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/about/AboutPage.vue"),
      },
    },
    {
      path: "/polls",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/polls/PollsPage.vue"),
      },
      children: [
        {
          name: "polls",
          path: ":n?",
          component: () => import("@/views/pages/polls/PollsList.vue"),
        },
      ],
    },
    {
      name: "rules",
      path: "/rules",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/rules/RulesPage.vue"),
      },
    },
    {
      name: "globalChat",
      path: "/chat",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/globalChat/GlobalChatPage.vue"),
      },
    },
    {
      path: "/messenger",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/messenger/MessengerPage.vue"),
      },
      children: [
        {
          name: "messenger",
          path: ":n?",
          component: () =>
            import("@/views/pages/messenger/ConversationsList.vue"),
        },
        {
          name: "conversation",
          path: "c/:id",
          component: () =>
            import("@/views/pages/messenger/ConversationView.vue"),
        },
        {
          name: "direct-message",
          path: "user/:login",
          component: () =>
            import("@/views/pages/messenger/DirectConversation.vue"),
        },
      ],
    },
    {
      name: "notifications",
      path: "/notifications",
      redirect: "/",
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
        page: () => import("@/views/pages/community/CommunityPage.vue"),
      },
      children: [
        {
          name: "community",
          path: ":n?",
          meta: { filter: "Active" },
          component: () => import("@/views/pages/community/UsersList.vue"),
        },
        {
          name: "community-all",
          path: "all/:n?",
          meta: { filter: "All" },
          component: () => import("@/views/pages/community/UsersList.vue"),
        },
        {
          name: "community-pending",
          path: "pending/:n?",
          meta: { filter: "Pending" },
          component: () => import("@/views/pages/community/UsersList.vue"),
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
      path: "/users/:login",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/profile/ProfilePage.vue"),
      },
    },
    // Legacy routes redirect to main profile
    {
      path: "/users/:login/games",
      redirect: (to) => ({ name: "profile", params: { login: to.params.login } }),
    },
    {
      path: "/users/:login/characters",
      redirect: (to) => ({ name: "profile", params: { login: to.params.login } }),
    },

    {
      path: "/forum/:id",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/forum/ForumPage.vue"),
      },
      children: [
        {
          name: "forum",
          path: ":n?",
          component: () => import("@/views/pages/forum/TopicsList.vue"),
        },
      ],
    },
    {
      path: "/topic/:id",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/topic/TopicPage.vue"),
      },
      children: [
        {
          name: "topic",
          path: ":n?",
          component: () => import("@/views/pages/topic/CommentsList.vue"),
        },
      ],
    },

    {
      path: "/games",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/games/GamesPage.vue"),
      },
      children: [
        {
          name: "games-active",
          path: "",
          meta: { status: "active" },
          component: () => import("@/views/pages/games/GamesList.vue"),
        },
        {
          name: "games-recruiting",
          path: "recruiting",
          meta: { status: "recruiting" },
          component: () => import("@/views/pages/games/GamesList.vue"),
        },
        {
          name: "games-finished",
          path: "finished",
          meta: { status: "finished" },
          component: () => import("@/views/pages/games/GamesList.vue"),
        },
        {
          name: "games-moderation",
          path: "moderation",
          meta: { status: "moderation" },
          component: () => import("@/views/pages/games/GamesList.vue"),
        },
      ],
    },
    {
      name: "blogs",
      path: "/blogs",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/blogs/BlogsPage.vue"),
      },
    },
    {
      name: "forum-index",
      path: "/forum",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/forum/ForumIndexPage.vue"),
      },
    },
    {
      path: "/moderation",
      meta: { requiresAuth: true },
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/moderation/ModerationPage.vue"),
      },
      children: [
        {
          name: "moderation",
          path: "",
          component: () => import("@/views/pages/moderation/ModerationOverview.vue"),
        },
        {
          name: "moderation-login-changes",
          path: "login-changes",
          component: () => import("@/views/pages/moderation/ModerationLoginChanges.vue"),
        },
      ],
    },
    {
      name: "create-game",
      path: "/create-game",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/create-game/CreateGamePage.vue"),
      },
    },
    {
      path: "/game/:id",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/game/GamePage.vue"),
      },
      children: [
        {
          name: "game",
          path: "",
          component: () => import("@/views/pages/game/GameDetails.vue"),
        },
        {
          name: "game-rooms",
          path: "rooms",
          component: () => import("@/views/pages/game/GameRooms.vue"),
        },
        {
          name: "game-room",
          path: "rooms/:roomId/:n?",
          component: () => import("@/views/pages/game/GameRoom.vue"),
        },
        {
          name: "game-characters",
          path: "characters",
          component: () => import("@/views/pages/game/GameCharacters.vue"),
        },
        {
          name: "game-comments",
          path: "comments/:n?",
          component: () => import("@/views/pages/game/GameComments.vue"),
        },
      ],
    },
    {
      name: "game-first-unread-post",
      path: "/game/:id/posts/unread",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/game/GameFirstUnreadPost.vue"),
      },
    },
    {
      name: "game-first-unread-comment",
      path: "/game/:id/comments/unread",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/game/GameFirstUnreadComment.vue"),
      },
    },
    // Auth callback for OAuth (Discord, etc.)
    {
      name: "auth-callback",
      path: "/auth/callback",
      components: {
        page: () => import("@/views/account/AuthCallback.vue"),
      },
    },
    // Session transfer from another mirror
    {
      name: "auth-transfer",
      path: "/auth/transfer",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/account/TransferPage.vue"),
      },
    },
    {
      name: "account",
      path: "/account",
      meta: { requiresAuth: true },
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/account/AccountPage.vue"),
      },
    },
    {
      name: "activation",
      path: "/activate/:token",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/account/ActivationPage.vue"),
      },
    },
    {
      name: "confirm-email",
      path: "/confirm-email/:token",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/account/EmailChangeConfirmPage.vue"),
      },
    },
    {
      name: "reset-password",
      path: "/reset-password/:token",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/account/ResetPasswordConfirm.vue"),
      },
    },
    // Dev tools
    {
      name: "theme-colors",
      path: "/dev/theme-colors",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/dev/ThemeColorsPage.vue"),
      },
    },
    {
      name: "dev-accounts",
      path: "/dev/accounts",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/dev/DevAccountsPage.vue"),
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
