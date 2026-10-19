<script setup lang="ts">
/** The menu of the sections, in the header. */
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
  column-gap: $big

.link
  font-size: $menu-font-size
  font-weight: normal
  letter-spacing: 1px
  color: $link-nav
  // A link never breaks into two lines: the menu wraps between links only.
  white-space: nowrap

  &:hover
    text-decoration: none
    color: $link-nav-hover
</style>
