<script setup lang="ts">
/**
 * SettingsSection — the boxed panel used across game/blog settings tabs
 * (blacklist, roles, invitations, info, rooms, rubrics). Owns the shared
 * `.settings-section` chrome so individual sections stop re-declaring it.
 * The heading is rendered through BlockTitle; pass `title` for a plain
 * string or use the #title slot for richer headings.
 */
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";

defineProps<{ title?: string }>();
</script>

<template>
  <section class="settings-section">
    <BlockTitle v-if="title || $slots.title">
      <slot name="title">{{ title }}</slot>
    </BlockTitle>
    <slot />
  </section>
</template>

<style scoped lang="sass">
.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius
</style>
