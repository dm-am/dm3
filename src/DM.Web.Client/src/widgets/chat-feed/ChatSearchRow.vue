<script setup lang="ts">
// Ctrl+F goes to the field while the row is on screen; a guest has none and keeps the browser's.
import { onBeforeUnmount, onMounted, ref } from "vue";
import {
  MessageSearchBar,
  type MessageSearchScope,
} from "@/features/message-search";
import { ChatDateJump } from "@/features/chat-date-jump";

withDefaults(
  defineProps<{
    /** The feed the row stands over, and the only one its field searches. */
    scope: MessageSearchScope;
    /** The day the feed is on, YYYY-MM-DD, or "" while live. */
    selectedDate?: string;
    /** The first day the feed has, YYYY-MM-DD, where it is known. */
    minDate?: string;
    /** The last day the feed has: today. */
    maxDate?: string;
  }>(),
  { selectedDate: "", minDate: "", maxDate: "" },
);

const emit = defineEmits<{
  /** A hit was picked: land on this message. */
  jump: [messageId: string];
  /** A day was picked in the calendar. */
  "date-picked": [date: string];
  /** The calendar opened. */
  "date-open": [];
}>();

const searchBar = ref<InstanceType<typeof MessageSearchBar> | null>(null);

function focusOnFind(event: KeyboardEvent) {
  if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== "f") {
    return;
  }
  if (!searchBar.value?.focus()) return;
  event.preventDefault();
}

onMounted(() => document.addEventListener("keydown", focusOnFind));

onBeforeUnmount(() => document.removeEventListener("keydown", focusOnFind));

/** Puts the caret in the field; false where there is none (a guest). */
function focus(): boolean {
  return searchBar.value?.focus() ?? false;
}

defineExpose({ focus });
</script>

<template>
  <!-- Over the list, never in it: the hits unroll from the row as a layer over the feed -->
  <div class="chat-search-row">
    <MessageSearchBar
      ref="searchBar"
      :scope="scope"
      @jump="(id) => emit('jump', id)"
    >
      <ChatDateJump
        :selected-date="selectedDate"
        :min-date="minDate"
        :max-date="maxDate"
        @date-picked="(date) => emit('date-picked', date)"
        @open="emit('date-open')"
      />
    </MessageSearchBar>
  </div>
</template>

<style scoped lang="sass">
// The least the field takes beside the button: room for a query, not only its placeholder.
$search-least: 160px

// The row breaks between its parts, never inside one: the button goes under the field.
.chat-search-row
  min-width: 0
  :deep(.filter-bar)
    flex-wrap: wrap
  :deep(.search-container)
    flex: 1 1 $search-least
    min-width: 0
</style>
