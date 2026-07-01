<script setup lang="ts">
/**
 * Unified "separator + Paging + separator" block with compact spacing.
 * Used on list pages (Testimonials, Polls, Pulse).
 */
import type { RouteLocationRaw } from "vue-router";
import Paging from "./Paging.vue";
import type { Paging as PagingType } from "@/shared/api/models/common";

defineProps<{
  paging: PagingType;
  to: RouteLocationRaw;
  useQuery?: boolean;
  queryKey?: string;
  onPrefetch?: (page: number) => void;
}>();
</script>

<template>
  <div v-if="paging && paging.pages > 1" class="paging-block">
    <div class="separator" aria-hidden="true">
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - -
    </div>
    <Paging
      :paging="paging"
      :to="to"
      :use-query="useQuery"
      :query-key="queryKey"
      :on-prefetch="onPrefetch"
    />
    <div class="separator" aria-hidden="true">
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
      - - - - - - - - - -
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.paging-block
  // Collapse Paging's default $medium top/bottom margin — compact look with separators
  :deep(.paging)
    margin: $tiny 0

.separator
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  max-width: 100%
  width: 0
  min-width: 100%
  user-select: none
</style>
