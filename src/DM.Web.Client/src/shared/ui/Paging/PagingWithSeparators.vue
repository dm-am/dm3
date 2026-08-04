<script setup lang="ts">
/**
 * Unified "separator + Paging + separator" block with compact spacing.
 * Used on list pages (Testimonials, Polls, Pulse).
 */
import type { RouteLocationRaw } from "vue-router";
import Paging from "./Paging.vue";
import type { PagingInfo } from "@/shared/api/models/common";

defineProps<{
  paging: PagingInfo;
  to: RouteLocationRaw;
  useQuery?: boolean;
  queryKey?: string;
  onPrefetch?: (page: number) => void;
  /** Forwarded to Paging: getter for the paginated block to scroll to. */
  scrollAnchor?: () => HTMLElement | null;
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
      :scroll-anchor="scrollAnchor"
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
// Digits have no descenders, so their ink sits ~1.5px above the geometric
// center of the line box — the gap to the dash line below reads larger than
// the one above. Nudge the row down by that amount so the ink-to-dash gaps
// match (measured at the 14px paging font).
$optical-shift: 1.5px

.paging-block
  // Collapse Paging's default margin — compact look with separators
  :deep(.paging)
    margin: ($tiny + $optical-shift) 0 ($tiny - $optical-shift)

.separator
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  max-width: 100%
  width: 0
  min-width: 100%
</style>
