<script setup lang="ts">
/**
 * The subscribers to one category of the user, under its tab. The names come
 * from a preview capped before categories are counted, so `total` is what the
 * line reports: names, a tail for the rest, or the count alone. A category
 * with nobody renders nothing. The names stand in the server's order.
 */
import { computed } from "vue";
import type { SubscriberRef } from "@/shared/api/models/common";

const props = defineProps<{
  subscribers: readonly SubscriberRef[];
  /** Heading shown above the list ("Подписаны на игры" etc.). */
  label: string;
  /** Bit flag from SubscriptionSettings (numeric bitmask). */
  flag: number;
  /** The category's real subscriber count, independent of the preview. */
  total: number;
}>();

const matching = computed(() =>
  props.subscribers.filter((s) => (s.settings & props.flag) !== 0),
);

/** How many of the category's subscribers the names do not cover. */
const untold = computed(() => Math.max(0, props.total - matching.value.length));
</script>

<template>
  <!-- One line under the tab's table, in the muted look of the rules page's links. -->
  <p v-if="total > 0" class="subscribers-line">
    <span class="subscribers-label">{{ label }}:</span>{{ " " }}
    <!-- No name of the preview carries this category, so the count is all there is to say. -->
    <template v-if="!matching.length">{{ total }}</template>
    <template v-else
      ><template v-for="(sub, idx) in matching" :key="sub.username"
        ><router-link
          :to="{ name: 'profile', params: { username: sub.username } }"
          >{{ sub.username }}</router-link
        ><template v-if="idx < matching.length - 1">, </template></template
      ><template v-if="untold > 0">... и еще {{ untold }}</template></template
    >
  </p>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

// One muted look: activity shows in the order, not the colour.
.subscribers-line
  margin: 0
  +muted-links-line

  .subscribers-label
    font-weight: 400

  :deep(a)
    +muted-link
</style>
