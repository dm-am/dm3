<script setup lang="ts">
/**
 * ModerationPanel — left-sidebar navigation over the moderation sections
 * (product doc 4.2.1.5). Mounted at the very top of the LEFT sidebar and
 * ONLY on /moderation/* routes for Moderator+ viewers (mount condition lives
 * in LeftSidebar, same pattern as GamePanel's route.meta gate).
 *
 * Pure navigation: links in the doc's order, "- " prefix idiom mirrored
 * from GamePanel/OwnedGames, Admin-only destinations filtered by the viewer's
 * role. Links are path-based so the panel stays inert (404 via the catch-all)
 * until every target page is wired, instead of throwing on unresolved route
 * names.
 */
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useAuthStore, userIsAdmin } from "@/entities/user";
import SidebarBlock from "./SidebarBlock.vue";

interface PanelLink {
  label: string;
  to: string;
  /** The destination page is Admin-gated (its useRoleGate row), hide below. */
  adminOnly?: boolean;
}

// Order and wording follow the doc verbatim.
const links: PanelLink[] = [
  { label: "Модерация", to: "/moderation/moderators" },
  { label: "Премодерируемые игры", to: "/moderation/games" },
  { label: "Премодерируемые блоги", to: "/moderation/blogs" },
  { label: "Последние баны", to: "/moderation/bans" },
  { label: "Последние предупреждения", to: "/moderation/warnings" },
  { label: "Последние оцененные посты", to: "/moderation/rated-posts" },
  { label: "Новые пользователи", to: "/moderation/new-users" },
  { label: "Нарушители", to: "/moderation/violators" },
  { label: "Поддержка", to: "/moderation/support", adminOnly: true },
  { label: "Жалобы", to: "/moderation/complaints" },
  { label: "Все загруженное", to: "/moderation/uploads" },
];

const { user } = storeToRefs(useAuthStore());
const visibleLinks = computed(() =>
  links.filter((link) => !link.adminOnly || userIsAdmin(user.value)),
);
</script>

<template>
  <SidebarBlock token="ModerationPanel">
    <template #title>Модерация</template>
    <li v-for="link in visibleLinks" :key="link.to" class="link">
      <span class="muted" aria-hidden="true">- </span>
      <router-link :to="link.to">{{ link.label }}</router-link>
    </li>
  </SidebarBlock>
</template>

<style scoped lang="sass">
.link
  display: block

.muted
  color: $text-muted
</style>
