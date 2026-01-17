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
      path: "/about",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/about/AboutPage.vue"),
      },
      children: [
        {
          name: "about",
          path: ":n?",
          component: () => import("@/views/pages/about/WebsiteReviewList.vue"),
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
          component: () => import("@/views/pages/messenger/ConversationsList.vue"),
        },
        {
          name: "conversation",
          path: "c/:id",
          component: () => import("@/views/pages/messenger/ConversationView.vue"),
        },
        {
          name: "direct-message",
          path: "user/:login",
          component: () => import("@/views/pages/messenger/DirectConversation.vue"),
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
      name: "all-games",
      path: "/games",
      components: {
        menu: GeneralMenu,
        sidebar: GeneralSidebar,
        page: () => import("@/views/pages/games/GamesPage.vue"),
      },
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
      component: () => import("@/components/TheLoader.vue"),
    },
    {
      name: "games",
      path: "/games/:status",
      component: () => import("@/components/TheLoader.vue"),
    },
    {
      name: "game",
      path: "/game/:id",
      component: () => import("@/components/TheLoader.vue"),
    },
    {
      name: "game-first-unread-post",
      path: "/game/:id/posts/unread",
      component: () => import("@/components/TheLoader.vue"),
    },
    {
      name: "game-comments",
      path: "/game/:id/comments",
      component: () => import("@/components/TheLoader.vue"),
    },
    {
      name: "game-characters",
      path: "/game/:id/characters",
      component: () => import("@/components/TheLoader.vue"),
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
