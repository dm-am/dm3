<script setup lang="ts">
// The admin tab strip. Tabs come from lib/sections — the table the pages under
// them gate themselves with — filtered by the viewer's role, so the strip
// never offers a tab whose page would answer with a refusal.
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { MODERATION_SECTIONS } from "./lib/sections";
import { hasRequiredRole } from "./lib/useRoleGate";

const { user } = storeToRefs(useAuthStore());

const sections = computed(() =>
  MODERATION_SECTIONS.filter((section) =>
    hasRequiredRole(user.value, section.role),
  ),
);
</script>

<template>
  <page-title v-once>Модерация</page-title>
  <nav v-if="sections.length" class="moderation-nav">
    <router-link
      v-for="section in sections"
      :key="section.name"
      :to="{ name: section.name }"
      >{{ section.label }}</router-link
    >
  </nav>
  <router-view />
</template>

<style scoped lang="sass">
.moderation-nav
  display: flex
  gap: $medium
  margin-bottom: $medium
  padding-bottom: $small
  border-bottom: 1px solid $border

  a
    color: $link
    text-decoration: none
    padding: $small $medium
    border-radius: $border-radius

    &:hover
      color: $link-hover

    &.router-link-active
      background: $bg-element-accent
      font-weight: bold
</style>
