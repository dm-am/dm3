<script setup lang="ts">
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";

defineProps<{ title?: string }>();
</script>

<template>
  <section class="settings-section">
    <BlockTitle v-if="title">{{ title }}</BlockTitle>
    <slot />
  </section>
</template>

<style scoped lang="sass">
// Square: a panel of content and not a control, the radius is the mark of what the reader operates.
.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element
</style>
