<script setup lang="ts">
// Placeholder page for a single blog (route name "blog", path /blogs/:id).
// No single-blog endpoint exists yet, so nothing is fetched here.
import { EmptyState } from "@/shared/ui/EmptyState";
</script>

<template>
  <page-title v-once>Блог</page-title>
  <EmptyState
    icon="pencil"
    title="Страница блога в разработке"
    hint="А пока вы можете вернуться к списку блогов"
  >
    <template #action>
      <router-link :to="{ name: 'blogs' }" class="back-link">
        <strong>К списку блогов</strong>
      </router-link>
    </template>
  </EmptyState>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.back-link
  color: $link
  &:hover
    color: $link-hover
</style>
