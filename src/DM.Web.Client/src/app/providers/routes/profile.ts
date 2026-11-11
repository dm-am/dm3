import type { RouteRecordRaw } from "vue-router";

// One component, the route name its key. The game pair rates whole games, the
// first pair single posts: two different numbers on the profile.
const SUBPAGES = [
  "received-reviews",
  "given-reviews",
  "received-endorsements",
  "given-endorsements",
  "received-game-reviews",
  "given-game-reviews",
] as const;

export const profileRoutes: RouteRecordRaw[] = [
  {
    name: "profile",
    path: "/users/:username",
    meta: { dynamicTitle: true },
    component: () => import("@/pages/profile/ProfilePage.vue"),
  },
  // Old tab-in-URL links: tabs are client state now.
  {
    path: "/users/:username/:tab(about|games|blogs|topics|achievements)",
    redirect: (to) => ({
      name: "profile",
      params: { username: to.params.username },
    }),
  },
  ...SUBPAGES.map((subpage): RouteRecordRaw => ({
    name: subpage,
    path: `/users/:username/${subpage}`,
    meta: { dynamicTitle: true },
    component: () => import("@/pages/profile/ProfileSubpage.vue"),
    props: { subpage },
  })),
  // Every condition but the session is the server's, asked by the page.
  {
    name: "write-endorsement",
    path: "/users/:username/write-endorsement",
    meta: { requiresAuth: true, dynamicTitle: true },
    component: () => import("@/pages/profile/ProfileEndorsementForm.vue"),
  },
  {
    name: "profile-uploads",
    path: "/users/:username/uploads",
    meta: { dynamicTitle: true },
    // The router has no roles: the page sends anybody but the owner or an
    // admin to "not found".
    component: () => import("@/pages/profile/ProfileUploadsPage.vue"),
  },
];
