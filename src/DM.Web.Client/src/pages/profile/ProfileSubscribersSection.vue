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
 * If no subscribers carry the requested flag, the section renders
 * nothing — the host page composes content + best-of + subscribers, and
 * a blank subscribers section would just add noise.
 */
import { computed } from "vue";
import type { SubscriberRef } from "@/shared/api/models/common";

const props = defineProps<{
  subscribers: readonly SubscriberRef[];
  /** Heading shown above the list ("Подписаны на игры" etc.). */
  label: string;
  /** Bit flag from SubscriptionSettings (numeric bitmask). */
  flag: number;
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
</script>

<template>
  <!-- One-line list of the people subscribed to this user's games / blogs /
       topics, with links to their profiles. Rendered below the tab's table;
       nothing shows when there are none. Style matches the rules page
       "Полезные ссылки" line — muted label + inline links, secondary size. -->
  <p v-if="matching.length" class="subscribers-line">
    <span class="subscribers-label">{{ label }}:</span>{{ " "
    }}<template v-for="(sub, idx) in matching" :key="sub.username"
      ><router-link
        :to="{ name: 'profile', params: { username: sub.username } }"
        >{{ sub.username }}</router-link
      ><template v-if="idx < matching.length - 1">, </template></template
    >
  </p>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

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
