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
      name: "chat",
      path: "/chat",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/chat/ChatPage.vue"),
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
      component: () => import("@/components/TheLoader.vue"),
    },
    {
      name: "donate",
      path: "/donate",
      component: () => import("@/components/TheLoader.vue"),
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
      component: () => import("@/components/TheLoader.vue"),
    },
    {
      path: "/profile",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/profile/ProfilePage.vue"),
      },
      children: [
        {
          name: "profile",
          path: "/:login",
          component: () => import("@/views/pages/profile/UserInformation.vue"),
        },
        {
          name: "user-games",
          path: "/:login/games",
          component: () => import("@/views/pages/profile/UserGames.vue"),
        },
        {
          name: "user-characters",
          path: "/:login/characters",
          component: () => import("@/views/pages/profile/UserCharacters.vue"),
        },
        {
          name: "user-settings",
          path: "/:login/settings",
          component: () => import("@/views/pages/profile/UserSettings.vue"),
        },
      ],
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
      name: "moderation",
      path: "/moderation",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/moderation/ModerationPage.vue"),
      },
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
      component: () => import("@/components/TheLoader.vue"),
    },
    // Auth callback for OAuth (Discord, etc.)
    {
      name: "auth-callback",
      path: "/auth/callback",
      components: {
        page: () => import("@/views/account/AuthCallback.vue"),
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

export default router;

export function extractNumberParam(
  param: string | string[],
  defaultValue: number = 1,
) {
  return parseInt(param as string) || defaultValue;
}
