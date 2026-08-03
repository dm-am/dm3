<script setup lang="ts">
/**
 * ChatEventLine — DEV-ONLY: the focal composite every strip variant leads
 * with, as ONE inline run of text nodes: state word, title, muted tail.
 *
 * Inline flow and not flex, because a flex row is copied with a newline
 * between every child: today's strip selects as three lines where one is
 * shown. Here a selection copies "Идет: Вечер быстрых зарисовок, до 22:48".
 *
 * `word` off reproduces the current site, where only a live event is labelled
 * and a scheduled one differs from it by font weight alone.
 */
import { computed } from "vue";
import { STATE_WORD, type MockEvent } from "./chatEventsMock";

const props = withDefaults(
  defineProps<{
    event: MockEvent;
    /** Print the state word. */
    word?: boolean;
  }>(),
  { word: true },
);

/** ", до 22:48, закрытый" — leads with a comma only when something follows. */
const tail = computed(() => {
  const parts = [
    props.event.timeText,
    props.event.isOpen ? "" : "закрытый",
  ].filter(Boolean);
  return parts.length ? `, ${parts.join(", ")}` : "";
});
</script>

<template>
  <span v-if="word" class="line-word"
    >{{ STATE_WORD[event.status] }}:{{ " " }}</span
  ><span class="line-title" :class="{ quiet: event.status !== 'Live' }">{{
    event.title
  }}</span
  ><span class="line-tail">{{ tail }}</span>
</template>

<style scoped lang="sass">
.line-word
  color: $text
  font-weight: 600

.line-title
  color: $text
  font-weight: 600

  // A scheduled or finished event does not shout; the word carries the state.
  &.quiet
    font-weight: 500

.line-tail
  color: $text-muted
</style>
