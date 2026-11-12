<script setup lang="ts" generic="T extends { id: string | number }">
/** The shell of the flat sidebar lists; `items === null` is "not loaded". */
import type { RouteLocationRaw } from "vue-router";
import SidebarBlock from "./SidebarBlock.vue";
import SidebarSkeleton from "./SidebarSkeleton.vue";
import SidebarForwardLink from "./SidebarForwardLink.vue";
import SidebarLoadFailure from "./SidebarLoadFailure.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";

defineProps<{
  token: string;
  title: string;
  /** The rows of the default placeholder; a #skeleton slot replaces it. */
  lines?: number;
  items: T[] | null;
  errored: boolean;
  empty: string;
  retry: () => void;
  forwardTo: RouteLocationRaw;
  forwardLabel: string;
}>();

defineSlots<{
  item(props: { item: T }): unknown;
  /** A placeholder shaped like the rows, where they are taller than a title. */
  skeleton?(): unknown;
}>();
</script>

<template>
  <SidebarBlock :token="token" :title="title">
    <slot v-if="items === null && !errored" name="skeleton">
      <SidebarSkeleton :lines="lines" />
    </slot>
    <li v-else-if="items === null" class="error-row">
      <SidebarLoadFailure @retry="retry" />
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
