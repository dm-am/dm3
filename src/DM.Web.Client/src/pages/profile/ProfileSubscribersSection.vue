<script setup lang="ts">
/**
 * ProfileSubscribersSection — subscribers list below a content tab's table
 * (Games / Blogs / Topics). Shows ONLY subscribers whose subscription
 * settings have the tab's category flag set ("subscribed to games", etc.),
 * so each tab tells the viewer who cares about that specific category.
 *
 * Styled like the rules page "Полезные ссылки" line (muted label + inline
 * muted links that light up on hover, secondary font-size) — see
 * RulesStaffTable.vue .useful-links.
 *
 * Ordering matches the profile-wide subscribers feed:
 *   - Active subscribers first, inactive (no activity in 30+ days) last
 *   - Inside each bucket, most-recently-active first
 *
 * The names come out of a preview the server caps at 20 and ranks by activity
 * BEFORE anything knows about categories, so they are a sample and not the
 * membership of this line: a category can be well populated and contribute few
 * names or none. `total` is the category's real count and is what the line
 * reports — names when it has them, a tail when it has fewer than it counts, and
 * the count alone when it has none. Same three cases, and the same wording, as
 * buildSubscribersTooltip in shared/lib/utils/tooltipBuilders.
 *
 * A category with no subscribers at all renders nothing: the host page composes
 * content + best-of + subscribers, and a blank line would just add noise.
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

const INACTIVITY_DAYS = 30;

function isInactive(lastActivityUtc: string | null): boolean {
  if (!lastActivityUtc) return true;
  const last = new Date(lastActivityUtc).getTime();
  if (Number.isNaN(last)) return true;
  return Date.now() - last > INACTIVITY_DAYS * 24 * 60 * 60 * 1000;
}

const matching = computed(() => {
  const list = props.subscribers.filter((s) => (s.settings & props.flag) !== 0);
  // Active first; within each bucket, most recently active first. Same
  // ordering rule as the page-wide subscribers feed — keeps the visual
  // "freshness gradient" predictable across the whole profile.
  return list.slice().sort((a, b) => {
    const ai = isInactive(a.lastActivityUtc) ? 1 : 0;
    const bi = isInactive(b.lastActivityUtc) ? 1 : 0;
    if (ai !== bi) return ai - bi;
    const ta = a.lastActivityUtc ? new Date(a.lastActivityUtc).getTime() : 0;
    const tb = b.lastActivityUtc ? new Date(b.lastActivityUtc).getTime() : 0;
    return tb - ta;
  });
});

/** How many of the category's subscribers the names do not cover. */
const untold = computed(() => Math.max(0, props.total - matching.value.length));
</script>

<template>
  <!-- One-line list of the people subscribed to this user's games / blogs /
       topics, with links to their profiles. Rendered below the tab's table;
       nothing shows when the category has none. Style matches the rules page
       "Полезные ссылки" line — muted label + inline links, secondary size. -->
  <p v-if="total > 0" class="subscribers-line">
    <span class="subscribers-label">{{ label }}:</span>{{ " " }}
    <!-- No name from the preview carries this category's flag, so the count is
         all there is to say. Better than the line disappearing while the
         category has subscribers. -->
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
@use "@/assets/styles/Inputs" as *

// The active/inactive subscriber distinction stays in the ordering
// (active first), not in the color — one uniform muted-links look.
.subscribers-line
  margin: 0
  +muted-links-line

  .subscribers-label
    font-weight: 500

  :deep(a)
    +muted-link
</style>
