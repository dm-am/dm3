import { createRouter, createWebHistory } from "vue-router";

import { LeftSidebar, RightSidebar } from "@/widgets/sidebar";
import { clearRegistry as clearExpandableRegistry } from "@/shared/lib/composables";

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
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/about/AboutPage.vue"),
      },
    },
    {
      name: "testimonials",
      path: "/testimonials",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/about/TestimonialsPage.vue"),
      },
    },
    {
      name: "polls",
      path: "/polls",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/community/PollsPage.vue"),
      },
    },
    {
      name: "rules",
      path: "/rules",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/rules/RulesPage.vue"),
      },
    },
    {
      name: "globalChat",
      path: "/chat",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/global-chat/GlobalChatPage.vue"),
      },
    },
    {
      path: "/messenger",
      meta: { requiresAuth: true },
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
      meta: { requiresAuth: true },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/personal/NotificationsPage.vue"),
      },
    },
    {
      name: "subscriptions",
      path: "/subscriptions",
      meta: { requiresAuth: true },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/personal/SubscriptionsPage.vue"),
      },
    },
    {
      name: "notepad",
      path: "/notepad",
      meta: { requiresAuth: true },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/personal/NotepadPage.vue"),
      },
    },
    {
      name: "community",
      path: "/community",
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
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/game/GamesPage.vue"),
      },
    },
    {
      name: "blogs",
      path: "/blogs",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/blog/BlogsPage.vue"),
      },
    },
    {
      name: "forum-index",
      path: "/forum",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/forum/ForumIndexPage.vue"),
      },
    },
    {
      path: "/moderation",
      meta: { requiresAuth: true },
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
      ],
    },
    {
      name: "create-game",
      path: "/create-game",
      meta: { requiresAuth: true },
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
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/account/TransferPage.vue"),
      },
    },
    {
      name: "account",
      path: "/account",
      meta: { requiresAuth: true },
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/account/AccountPage.vue"),
      },
    },
    {
      name: "activation",
      path: "/activate/:token",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/account/AccountActivationPage.vue"),
      },
    },
    {
      name: "confirm-email",
      path: "/confirm-email/:token",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/account/EmailChangePage.vue"),
      },
    },
    {
      name: "reset-password",
      path: "/reset-password/:token",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/account/PasswordResetPage.vue"),
      },
    },
    {
      name: "support",
      path: "/support",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/support/SupportPage.vue"),
      },
    },
    {
      name: "complaint",
      path: "/complaint",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/support/ComplaintPage.vue"),
      },
    },
    {
      name: "pulse",
      path: "/pulse",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/pulse/PulsePage.vue"),
      },
    },
    {
      name: "warnings",
      path: "/warnings",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/warnings/WarningsPage.vue"),
      },
    },
    {
      name: "privacy-policy",
      path: "/privacy",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/legal/PrivacyPolicyPage.vue"),
      },
    },
    {
      name: "user-agreement",
      path: "/agreement",
      components: {
        left: LeftSidebar,
        right: RightSidebar,
        page: () => import("@/pages/legal/UserAgreementPage.vue"),
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

// Belt-and-suspenders cleanup: wipe the expandable registry on every
// navigation so stale handles from the previous page never appear in
// ScrollNav's "Развернуть все" count. Individual components still
// unregister via onBeforeUnmount; this is defence-in-depth for detached
// DOM and hmr edge cases.
router.afterEach(() => {
  clearExpandableRegistry();
});

export default router;

export function extractNumberParam(
  param: string | string[],
  defaultValue: number = 1,
) {
  return parseInt(param as string) || defaultValue;
}
