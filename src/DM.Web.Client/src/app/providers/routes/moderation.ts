import type { RouteRecordRaw } from "vue-router";

import {
  MODERATION_SECTIONS,
  type ModerationSectionName,
} from "@/shared/config/moderationSections";

// The tab reads what the menu row reads.
const titleOf = (name: ModerationSectionName) =>
  MODERATION_SECTIONS.find((section) => section.name === name)?.label;

export const moderationRoutes: RouteRecordRaw[] = [
  {
    // One record owns the zone: the sign-in, the sidebar menu and the 403 of
    // ModerationPage for a viewer below the section's role.
    path: "/moderation",
    meta: { requiresAuth: true, moderationZone: true },
    component: () => import("@/pages/moderation/ModerationPage.vue"),
    children: [
      // A section every moderator may open.
      {
        path: "",
        redirect: { name: "moderation-moderators" },
      },
      {
        name: "moderation-moderators",
        path: "moderators",
        meta: { title: titleOf("moderation-moderators") },
        component: () => import("@/pages/moderation/ModerationModerators.vue"),
      },
      {
        name: "moderation-games",
        path: "games",
        meta: { title: titleOf("moderation-games") },
        component: () =>
          import("@/pages/moderation/ModerationPremoderatedGames.vue"),
      },
      {
        name: "moderation-blogs",
        path: "blogs",
        meta: { title: titleOf("moderation-blogs") },
        component: () =>
          import("@/pages/moderation/ModerationPremoderatedBlogs.vue"),
      },
      {
        // Open to everyone, a guest too, as the warning log and the pulse are:
        // the ban list is the site's public record. No sign-in, so an expired
        // session leaves the reader on it, and the shell opens it to every role.
        name: "moderation-bans",
        path: "bans",
        meta: { title: titleOf("moderation-bans"), requiresAuth: false },
        component: () => import("@/pages/moderation/ModerationBans.vue"),
      },
      {
        name: "moderation-warnings",
        path: "warnings",
        meta: { title: titleOf("moderation-warnings") },
        component: () => import("@/pages/moderation/ModerationWarnings.vue"),
      },
      {
        name: "moderation-rated-posts",
        path: "rated-posts",
        meta: { title: titleOf("moderation-rated-posts") },
        component: () => import("@/pages/moderation/ModerationRatedPosts.vue"),
      },
      {
        name: "moderation-new-users",
        path: "new-users",
        meta: { title: titleOf("moderation-new-users") },
        component: () => import("@/pages/moderation/ModerationNewUsers.vue"),
      },
      {
        name: "moderation-violators",
        path: "violators",
        meta: { title: titleOf("moderation-violators") },
        component: () => import("@/pages/moderation/ModerationViolators.vue"),
      },
      {
        name: "moderation-username-changes",
        path: "username-changes",
        meta: { title: titleOf("moderation-username-changes") },
        component: () =>
          import("@/pages/moderation/ModerationUsernameChanges.vue"),
      },
      {
        name: "moderation-removed-accounts",
        path: "removed-accounts",
        meta: { title: titleOf("moderation-removed-accounts") },
        component: () =>
          import("@/pages/moderation/ModerationRemovedAccounts.vue"),
      },
      {
        name: "moderation-tickets",
        path: "tickets",
        meta: { title: titleOf("moderation-tickets") },
        component: () => import("@/pages/moderation/ModerationTickets.vue"),
      },
      {
        name: "moderation-ticket",
        path: "tickets/:ticketId",
        meta: { dynamicTitle: true },
        component: () => import("@/pages/moderation/ModerationTicketPage.vue"),
      },
      {
        name: "moderation-uploads",
        path: "uploads",
        meta: { title: titleOf("moderation-uploads") },
        component: () => import("@/pages/moderation/ModerationUploads.vue"),
      },
      {
        name: "moderation-tags",
        path: "tags",
        meta: { title: titleOf("moderation-tags") },
        component: () => import("@/pages/moderation/ModerationTags.vue"),
      },
      {
        name: "moderation-schema-templates",
        path: "schema-templates",
        meta: { title: titleOf("moderation-schema-templates") },
        component: () =>
          import("@/pages/moderation/ModerationSchemaTemplates.vue"),
      },
      // One page for both: the static segment comes first so it is not an id.
      {
        name: "moderation-schema-template-new",
        path: "schema-templates/new",
        meta: { title: titleOf("moderation-schema-templates") },
        component: () =>
          import("@/pages/moderation/ModerationSchemaTemplate.vue"),
      },
      {
        name: "moderation-schema-template",
        path: "schema-templates/:id",
        meta: { title: titleOf("moderation-schema-templates") },
        component: () =>
          import("@/pages/moderation/ModerationSchemaTemplate.vue"),
      },
      {
        name: "moderation-awards",
        path: "awards",
        meta: { title: titleOf("moderation-awards") },
        component: () => import("@/pages/moderation/ModerationAwards.vue"),
      },
      {
        name: "moderation-awards-series",
        path: "awards/series/:id",
        meta: { title: "Серия конкурсов" },
        component: () =>
          import("@/pages/moderation/ModerationAwardsSeries.vue"),
      },
      {
        name: "moderation-award-types",
        path: "award-types",
        meta: { title: "Каталог типов наград" },
        component: () => import("@/pages/moderation/ModerationAwardTypes.vue"),
      },
      {
        name: "moderation-achievements",
        path: "achievements",
        meta: { title: titleOf("moderation-achievements") },
        component: () =>
          import("@/pages/moderation/ModerationAchievements.vue"),
      },
      {
        name: "moderation-fundraising",
        path: "fundraising",
        meta: { title: titleOf("moderation-fundraising") },
        component: () => import("@/pages/moderation/FundraisingPage.vue"),
      },
    ],
  },
];
