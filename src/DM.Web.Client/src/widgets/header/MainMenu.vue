<script setup lang="ts">
/**
 * The menu of the site's sections, one look wherever it stands: in the
 * header of a desktop, and at the top of the sliding column of a narrow
 * screen, where it is the menu of the header moved there and has to read as
 * that menu. Large grey links from the left edge, set apart by their gaps
 * and the 1px tracking DM2 gives them; the lines wrap between links, never
 * inside one.
 *
 * The size of the type and the gap are custom properties, --menu-font and
 * --menu-gap: the header steps them with its width (Header.vue), and where
 * nobody sets them the menu takes its full size.
 */
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useAuthStore, userIsModerator } from "@/entities/user";
import { visibleMainNavSections } from "@/shared/config/mainNavigation";

const { user } = storeToRefs(useAuthStore());
const sections = computed(() =>
  visibleMainNavSections(userIsModerator(user.value)),
);
</script>

<template>
  <nav class="main-menu" aria-label="Основные разделы">
    <router-link
      v-for="section in sections"
      :key="section.key"
      class="link"
      :to="section.to"
      >{{ section.title }}</router-link
    >
  </nav>
</template>

<style scoped lang="sass">
.main-menu
  display: flex
  flex-wrap: wrap
  justify-content: flex-start
  align-items: center
  column-gap: var(--menu-gap, #{$big})

.link
  font-size: var(--menu-font, #{$menu-font-size})
  font-weight: normal
  letter-spacing: 1px
  color: $link-nav
  // A link never breaks into two lines: the menu wraps between links only.
  white-space: nowrap

  &:hover
    text-decoration: none
    color: $link-nav-hover
</style>
