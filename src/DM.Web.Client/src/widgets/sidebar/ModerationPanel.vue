<script setup lang="ts">
/**
 * ModerationPanel — the menu of moderation in the left sidebar (product doc
 * 4.2.1.5). Mounted first in the column and ONLY on /moderation/* routes for
 * Moderator+ viewers (the mount condition lives in LeftSidebar, the same
 * pattern as GamePanel's route.meta gate).
 *
 * Pure navigation, built from the one table of sections
 * (shared/config/moderationSections.ts): its groups in their order and in each
 * group its rows, each offered only to the viewer its page opens to, so the
 * menu cannot name a section the zone would answer with the 403 page. No
 * heading stands over a group: an empty line parts two groups, as on DM2 the
 * groups of the menu of a game are parted. A group the viewer has no row in
 * leaves no line behind it.
 *
 * The section the address names by its route is bold. A page inside a section
 * (a series of contests, the catalogue of award types, a template, a ticket)
 * marks nothing: its address names no row, and its heading says where the
 * reader is.
 */
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useRoute } from "vue-router";
import {
  useAuthStore,
  userIsAdmin,
  userIsModerator,
  userIsSeniorModerator,
} from "@/entities/user";
import {
  MODERATION_GROUPS,
  MODERATION_SECTIONS,
  type ModerationRole,
} from "@/shared/config/moderationSections";
import { MenuLink } from "@/shared/ui/MenuLink";
import SidebarBlock from "./SidebarBlock.vue";

const { user } = storeToRefs(useAuthStore());
const route = useRoute();

/** Whether a viewer holds a role, for each role a section can ask for. */
const HOLDS: Record<ModerationRole, (viewer: typeof user.value) => boolean> = {
  Moderator: userIsModerator,
  SeniorModerator: userIsSeniorModerator,
  Admin: userIsAdmin,
};

/**
 * The rows the viewer is offered, group by group, the first row of every
 * group but the first marked: the empty line stands over it.
 */
const rows = computed(() => {
  const visible = MODERATION_SECTIONS.filter((section) =>
    HOLDS[section.role](user.value),
  );
  return MODERATION_GROUPS.flatMap((group) =>
    visible.filter((section) => section.group === group),
  ).map((section, index, all) => ({
    ...section,
    startsGroup: index > 0 && all[index - 1].group !== section.group,
  }));
});

function currentOf(name: string): "page" | undefined {
  return route.name === name ? "page" : undefined;
}
</script>

<template>
  <SidebarBlock token="ModerationPanel" title="Модерация">
    <li
      v-for="section in rows"
      :key="section.name"
      :class="{ 'group-start': section.startsGroup }"
    >
      <MenuLink
        :to="{ name: section.name }"
        :current="currentOf(section.name)"
        >{{ section.label }}</MenuLink
      >
    </li>
  </SidebarBlock>
</template>

<style scoped lang="sass">
// DM2's empty line between two groups of a menu: one line of the menu's
// own height over the first row of a group.
.group-start
  margin-top: 1lh
</style>
