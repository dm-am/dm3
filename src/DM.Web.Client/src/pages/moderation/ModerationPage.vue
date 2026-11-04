<script setup lang="ts">
/**
 * The shell of every moderation route, and its one job is the door: a viewer
 * below the role of the section the address names gets the site's 403 page in
 * the section's place. The role comes from the same table the menu filters
 * itself by, so the menu never offers a section this door would close.
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/shared/stores";
import {
  moderationPageRole,
  type ModerationRole,
} from "@/shared/config/moderationSections";
import {
  userIsAdmin,
  userIsModerator,
  userIsSeniorModerator,
} from "@/entities/user";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { ErrorPage, getErrorConfig } from "@/shared/ui/ErrorPage";

const route = useRoute();
const { user } = storeToRefs(useAuthStore());

const HAS_ROLE: Record<ModerationRole, typeof userIsModerator> = {
  Moderator: userIsModerator,
  SeniorModerator: userIsSeniorModerator,
  Admin: userIsAdmin,
};

/**
 * Whether the section behind the address is closed to this viewer. A route of
 * the zone missing from the table opens to the zone's floor, a moderator, as
 * the menu does; routes/moderation.spec.ts keeps every route of the zone in the
 * table.
 */
const refused = computed(() => {
  // A section its route opens to everyone (the ban list) has no door.
  if (route.meta.requiresAuth === false) return false;
  const required = moderationPageRole(String(route.name ?? "")) ?? "Moderator";
  return !HAS_ROLE[required](user.value);
});

/**
 * The tab: the error's words while the 403 page stands, the section's static
 * title otherwise. The getter reads the section too, because that read is what
 * writes the tab again on a step from one closed section to another, after the
 * router has put the section's own title there.
 */
const tabTitle = computed(() =>
  refused.value ? getErrorConfig(403).title : route.meta.title,
);
const section = computed(() => route.name);
useDocumentTitle(() => {
  void section.value;
  return tabTitle.value;
});
</script>

<template>
  <ErrorPage v-if="refused" :code="403" />
  <router-view v-else />
</template>
