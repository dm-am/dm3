<script setup lang="ts">
/**
 * ModerationPage — the shell of every moderation route, and its one job is the
 * door. It draws nothing of its own: each section titles itself with the words
 * of its menu row, and the menu lives in the left sidebar.
 *
 * A viewer below the role of the section the address names gets the 403 page of
 * the site, whole, in the section's place. A moderator who typed the address of
 * a senior's section, and a player who followed an old link, used to read a
 * line of refusal under the heading of a page that was not his: what a reader
 * may not open is not drawn at all. The role comes from the
 * table of sections, the same row the menu filters itself by, so the menu never
 * offers a section this door would close. The server refuses the data either way.
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { moderationPageRole } from "@/shared/config/moderationSections";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { ErrorPage, getErrorConfig } from "@/shared/ui/ErrorPage";
import { hasRequiredRole } from "./lib/useRoleGate";

const route = useRoute();
const { user } = storeToRefs(useAuthStore());

/**
 * Whether the section behind the address is closed to this viewer. A route of
 * the zone missing from the table opens to the zone's floor, a moderator, as
 * the menu does; router.spec.ts keeps every route of the zone in the table.
 */
const refused = computed(() => {
  const required = moderationPageRole(String(route.name ?? "")) ?? "Moderator";
  return !hasRequiredRole(user.value, required);
});

/**
 * The tab: the error's words while the 403 page stands, the section's static
 * title otherwise. A section titled by its data (one ticket) has none here and
 * writes its own after this, the way it does under the router.
 *
 * Written on a change of these words or of the section, and on nothing else.
 * A page of a list or another ticket keeps both, so a title the page composed
 * is never written over; and a step from one closed section to another changes
 * only the section, after the router has just put that section's own title in
 * the tab over the 403 page. That is why the getter reads the section as well
 * as the words it returns: the read alone is what writes the tab again. Both
 * are computed, since the route itself changes on every step, a query too.
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
