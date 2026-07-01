<script setup lang="ts">
/**
 * ChatEventBanner — read-only chip for the global chat event (guest scope).
 *
 * Sits on the chat top bar next to the date picker and is styled to match the
 * date-picker button (same height, radius, border, neutral colors) — a plain
 * info chip, not a decorated banner. Renders nothing when there is no
 * upcoming/live event or on load failure.
 */
import { onMounted, ref, computed } from "vue";
import dayjs from "dayjs";
import { globalChatApi } from "@/entities/global-chat";
import type {
  GlobalChatEventStatus,
  GlobalChatEventSummary,
} from "@/entities/global-chat";

const event = ref<GlobalChatEventSummary | null>(null);

const statusLabels: Record<GlobalChatEventStatus, string> = {
  Scheduled: "Запланировано",
  Live: "В эфире",
  Ended: "Завершено",
};

const statusLabel = computed(() =>
  event.value ? statusLabels[event.value.status] : "",
);

const startsLabel = computed(() =>
  event.value
    ? dayjs(event.value.startsUtc).format("DD.MM.YYYY [в] HH:mm")
    : "",
);

onMounted(async () => {
  const { data, error } = await globalChatApi.getEvents();
  // Silent on failure — the chip is supplementary, not primary content.
  if (error) return;
  const list = data?.resources ?? [];
  // Show the live event if one is running, otherwise the soonest upcoming
  // (scheduled) one. Ended events are not surfaced.
  const live = list.find((e) => e.status === "Live");
  const upcoming = list
    .filter((e) => e.status === "Scheduled")
    .sort((a, b) => (a.startsUtc < b.startsUtc ? -1 : 1))[0];
  event.value = live ?? upcoming ?? null;
});
</script>

<template>
  <!-- Explicit literal separators (": ", ", ") so a copied selection reads
       "Запланировано: Литературный вечер, 16.06.2026 в 01:55". Styled to match
       the sibling date-picker button — same height/radius/border and neutral
       colors, no accent/dashed/gold decoration. -->
  <div v-if="event" class="chat-event">
    <span>{{ statusLabel }}</span
    >{{ ": " }}<span class="event-title">{{ event.title }}</span
    >{{ ", " }}<span>{{ startsLabel }}</span>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Variables"

// Matches the date-picker trigger (+button): same height, padding, radius,
// border and surface — but non-interactive (no hover/cursor).
.chat-event
  display: inline-flex
  align-items: center
  height: $control-height
  box-sizing: border-box
  padding: $button-padding
  font-size: $secondary-font-size
  line-height: 1
  white-space: nowrap
  color: $text
  background-color: $bg-element
  border: 1px solid $border
  border-radius: $button-border-radius

.event-title
  font-weight: bold
</style>
