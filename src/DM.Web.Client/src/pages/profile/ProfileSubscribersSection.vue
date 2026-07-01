<script setup lang="ts">
/**
 * ProfileSubscribersSection — inline subscribers list inside a content
 * tab (Games / Blogs / Topics). Shows ONLY subscribers whose subscription
 * settings have the tab's category flag set ("subscribed to games", etc.),
 * so each tab tells the viewer who cares about that specific category.
 *
 * Ordering and inactive styling match the rules established by the
 * profile-wide subscribers feed:
 *   - Active subscribers first, inactive (no activity in 30+ days) last
 *   - Inside each bucket, most-recently-active first
 *   - Inactive subscribers render in muted gray; hover restores the link
 *     color so the affordance is consistent with the site-wide a:hover
 *
 * If no subscribers carry the requested flag, the section renders
 * nothing — the host page composes content + best-of + subscribers, and
 * a blank subscribers section would just add noise.
 */
import { computed } from "vue";
import type { SubscriberRef } from "@/shared/api/models/common";
import LeadText from "@/shared/ui/Layout/LeadText.vue";

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
       topics, with links to their profiles (LeadText style). Rendered at the
       top of the content tab; nothing shows when there are none. -->
  <LeadText v-if="matching.length" class="subscribers-line">
    {{ label }}:
    <template v-for="(sub, idx) in matching" :key="sub.username"
      ><router-link
        :to="{ name: 'profile', params: { username: sub.username } }"
        >{{ sub.username }}</router-link
      ><span v-if="idx < matching.length - 1">, </span></template
    >
  </LeadText>
</template>

<style scoped lang="sass">
// The tab-content's flex `gap` already spaces this line from the table below;
// drop LeadText's own bottom margin so the gap isn't doubled. Two classes beat
// LeadText's single-class rule, so this reliably wins.
.lead-text.subscribers-line
  margin-bottom: 0
</style>
