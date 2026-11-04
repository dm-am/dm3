<script setup lang="ts">
/** The menu of moderation, from the one table of sections; each row only for whom its page opens. */
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

/** The rows offered, group by group, each group's first row marked for the empty line over it. */
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
// DM2's empty line between two groups of a menu.
.group-start
  margin-top: 1lh
</style>
