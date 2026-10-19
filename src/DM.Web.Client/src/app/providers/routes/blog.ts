import type { RouteRecordRaw } from "vue-router";

export const blogRoutes: RouteRecordRaw[] = [
  {
    name: "blogs",
    path: "/blogs",
    meta: { title: "Блоги" },
    component: () => import("@/pages/blog/BlogsPage.vue"),
  },
  {
    name: "create-blog",
    path: "/blogs/create",
    // No requiresAuth: the page renders its own login invitation for guests
    meta: { title: "Новый блог" },
    component: () => import("@/pages/create-blog/CreateBlogPage.vue"),
  },
  {
    // BlogPage is the zone shell: the tab reads the blog name, then the
    // section of the sub-route; the root has none, hence dynamicTitle.
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
        // Not requiresAuth: anybody but the owner and assistants, a guest
        // included, gets "not found" rather than the login window.
        name: "blog-publication-create",
        path: "feed/create",
        meta: { section: "Создание публикации" },
        component: () => import("@/pages/blog/PublicationCreate.vue"),
      },
      {
        // Named by its title, which is data (useZoneSection).
        name: "blog-publication",
        path: "feed/:pubId",
        meta: { dynamicTitle: true },
        component: () => import("@/pages/blog/PublicationPage.vue"),
      },
      {
        // No requiresAuth, as on the creation of one above.
        name: "blog-publication-edit",
        path: "feed/:pubId/edit",
        meta: { section: "Редактирование публикации" },
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
  // A notification names the blog in the readable-guid form, which the blog
  // endpoint does not take: the blog is read off the publication instead.
  {
    name: "publication-redirect",
    path: "/publication/:pubId",
    meta: { title: "Переход к публикации" },
    component: () => import("@/pages/redirect/PublicationRedirect.vue"),
  },
];
