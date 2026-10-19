import type { RouteRecordRaw } from "vue-router";

export const forumRoutes: RouteRecordRaw[] = [
  {
    // One parent for every level, so the board strip stays one live instance
    // and its reorder animation is seen.
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
      // A sibling of the topic, not a child: under TopicPage the topic would
      // be marked read before the server said where the reader stopped.
      {
        name: "topic-unread",
        path: ":alias/:num/unread",
        meta: { title: "Переход к непрочитанному комментарию" },
        component: () => import("@/pages/forum/TopicUnread.vue"),
      },
    ],
  },
  // Links that carry only a topic id; replaced with /forum/:alias/:num.
  {
    name: "forum-topic-redirect",
    path: "/forum-topic/:topicId",
    meta: { title: "Переход к топику" },
    component: () => import("@/pages/redirect/TopicRedirect.vue"),
  },
];
