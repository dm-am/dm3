<script setup lang="ts">
/**
 * ChatEventRun — DEV-ONLY: the standard left run of the strip, shared by every
 * variant that keeps the one-line composition — focal event, an optional extra
 * item, and the quiet count of everything else.
 *
 * The whole run is one inline flow with literal " | " text nodes, so it copies
 * as "Идет: Вечер быстрых зарисовок, до 22:48 | описание | +2 запланировано,
 * ближайший 05.08" and not as a stack of lines.
 *
 * The narrow rules live here and nowhere else: below 600px of frame the run
 * drops the two parts that carry the least ("запланировано" and the nearest
 * date), which is what keeps the title on screen instead of squeezing it to
 * nothing. The frame is the size container, so the width switch above the
 * catalog fires them.
 */
import ChatEventLine from "./ChatEventLine.vue";
import type { MockEvent } from "./chatEventsMock";

withDefaults(
  defineProps<{
    event: MockEvent | null;
    /** How many upcoming events are not on the line. */
    restCount: number;
    /** DD.MM of the nearest upcoming event, empty when it is the focal one. */
    nearest?: string;
    /** Print the state word. */
    word?: boolean;
  }>(),
  { nearest: "", word: true },
);
</script>

<template>
  <span v-if="event"
    ><ChatEventLine :event="event" :word="word" /><template v-if="$slots.extra"
      ><span class="run-sep" aria-hidden="true">{{ " | " }}</span
      ><slot name="extra" /></template
    ><template v-if="restCount > 0"
      ><span class="run-sep" aria-hidden="true">{{ " | " }}</span
      ><button type="button" class="run-count">
        +{{ restCount
        }}<span class="run-count-tail">{{ " запланировано" }}</span></button
      ><span v-if="nearest" class="run-near"
        >, ближайший {{ nearest }}</span
      ></template
    ></span
  >
  <span v-else class="run-none">Нет запланированных эвентов</span>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.run-sep
  color: $text-muted

// Calm at rest so the strip stays one colour, link-blue on intent.
.run-count
  +inline-link-button
  &
    font-size: $secondary-font-size
    white-space: nowrap
    color: $text-muted
  &:hover:not(:disabled)
    color: $link
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: 2px

.run-near
  color: $text-muted

.run-none
  color: $text-muted

// Narrow frame: the two least load-bearing parts go first, so the title is
// never the thing that disappears.
@container (max-width: 600px)
  .run-count-tail,
  .run-near
    display: none
</style>
