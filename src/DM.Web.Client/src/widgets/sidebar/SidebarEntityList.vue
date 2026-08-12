<script setup lang="ts" generic="T extends { id: string | number }">
/**
 * SidebarEntityList — the shared shell for the flat sidebar list blocks
 * (Active/Recruiting/Finished games, Active blogs). It owns the common
 * skeleton / error-row / empty / list / forward-link scaffolding; the wrapper
 * keeps its store, fetch and watchers and supplies each row through the #item
 * slot plus the block's copy and forward target.
 *
 * `items === null` means "not loaded": a skeleton shows while `errored` is
 * false, and the retry error-row shows once the fetch has failed. `lines` is
 * the height of that skeleton in rows, and it is the one thing the blocks
 * disagree on: the popular lists stand ten rows deep, the recruiting one
 * fifteen, the rest five.
 *
 * The Owned* blocks deliberately do NOT use this shell, and the difference is
 * not the grouping (OwnedBlogs has none): they read "not loaded" off a loading
 * flag rather than off a null list, and their failure row is bare text with no
 * retry button. Their markup is kept local.
 */
import type { RouteLocationRaw } from "vue-router";
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SidebarForwardLink from "./SidebarForwardLink.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";

defineProps<{
  token: string;
  title: string;
  lines: number;
  items: T[] | null;
  errored: boolean;
  empty: string;
  retry: () => void;
  forwardTo: RouteLocationRaw;
  forwardLabel: string;
}>();

defineSlots<{ item(props: { item: T }): unknown }>();
</script>

<template>
  <SidebarBlock :token="token" :title="title">
    <template #title>{{ title }}</template>
    <SidebarSkeleton v-if="items === null && !errored" :lines="lines" />
    <li v-else-if="items === null" class="error-row">
      <SecondaryText
        >Не удалось загрузить
        <button type="button" class="retry" @click="retry">
          Повторить
        </button></SecondaryText
      >
    </li>
    <li v-else-if="items.length === 0">
      <SecondaryText>{{ empty }}</SecondaryText>
    </li>
    <template v-else>
      <template v-for="item in items" :key="item.id">
        <slot name="item" :item="item" />
      </template>
    </template>
    <li><DashSeparator spacing="tiny" width="75%" /></li>
    <SidebarForwardLink :to="forwardTo">{{ forwardLabel }}</SidebarForwardLink>
  </SidebarBlock>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.retry
  margin-left: $small
  +inline-link-button
</style>
