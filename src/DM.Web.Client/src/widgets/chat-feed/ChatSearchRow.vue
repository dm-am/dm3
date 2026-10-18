<script setup lang="ts">
/**
 * ChatSearchRow: the search row of a feed. Above the frame of the global chat,
 * and under the line at the top of a correspondence, where "поиск" of the line
 * opens it. The field searches the feed it belongs to (`scope`: "Поиск по
 * чату", "Поиск по переписке"), and "Перейти к дате" at the end of the row
 * opens the feed at a day. A hit and a day are the page's to land on (`jump`,
 * `date-picked`); the window logic is useChatHistoryWindow.
 *
 * Both parts keep their words at every width. Where the field would have too
 * little room for a query beside the button, the button stands under the
 * field instead.
 *
 * Ctrl+F (Cmd+F on macOS) goes to the row's field instead of the browser's
 * find while the row is on screen: there is nothing to open, only somewhere
 * to put the caret. A guest has no field (the endpoint refuses him), and then
 * the key is left to the browser rather than swallowed.
 */
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
  <!-- Above the frame of the global chat, under the line at the top of a
       correspondence: either way over the list of messages, never in it,
       since the hits unroll from the row as a layer over the feed. -->
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
// The least the field takes beside the button: its frame, the magnifier and
// room for a query, not only for the words of its placeholder ("Поиск по
// переписке" is 124 in the site's face at the field's 14).
$search-least: 160px

// The row gives way to the column it stands in, and keeps both parts whole.
// The field and "Перейти к дате" share one line wherever the field keeps its
// least beside the button. Where it would not (the middle column of a window
// just over 1000, a correspondence beside a wide list), the button stands
// under the field, at its start, as a second line of the row: the row breaks
// between its parts and never inside one. Kept to one line there, the field
// went down to 80 and cut its own placeholder. The calendar of the date hangs
// from the row, so it keeps within it on two lines as on one (ChatDateJump).
.chat-search-row
  min-width: 0
  :deep(.filter-bar)
    flex-wrap: wrap
  :deep(.search-container)
    flex: 1 1 $search-least
    min-width: 0
</style>
