<script setup lang="ts">
/**
 * MessageSearchBar — the global chat's search row.
 *
 * The site has exactly one search idiom, the .filter-bar line: a growing
 * FilterSearchInput followed by $control-height buttons (forum, games, blogs,
 * comments, polls, pulse, users all assemble it the same way). This is that
 * line, applied to the chat. It stands ABOVE the chat frame — the search is
 * not part of the feed — and its results unroll as a dropdown laid over the
 * feed: picking a row jumps the feed to that message and the dropdown stays
 * open, so the next hit is one click away.
 *
 * Scope is the global chat and nothing else (see searchStore), order is the
 * backend's only one, newest first — hence no scope selector and no sort
 * control.
 *
 * The default slot carries the rest of the row (the chat's "Перейти к дате"
 * button), so the field and the buttons share one line.
 *
 * The endpoint is authenticated, so a guest gets the row without the field
 * rather than a 401 that would throw him off the page.
 */
import { ref } from "vue";
import { storeToRefs } from "pinia";
import { vClickOutside } from "@/shared/directives";
import { FilterSearchInput } from "@/shared/ui/Filters";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useAuthStore } from "@/shared/stores";
import { useMessageSearchStore } from "../model/searchStore";
import type { MessageSearchResult } from "../model/types";

const emit = defineEmits<{
  /** Land on this message in the live feed (the page owns the feed). */
  jump: [messageId: string];
}>();

const store = useMessageSearchStore();
const { query, results, hasMore, loading, loadingMore, error, hasSearched } =
  storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

const rootRef = ref<HTMLElement | null>(null);

/** The results layer is up or down; the query itself outlives it. */
const resultsOpen = ref(false);

/**
 * Applied by the shared filter-search composable — the same 300ms debounce,
 * the same apply-on-blur, that every other search field on the site uses.
 */
function runSearch(value: string) {
  store.query = value;
  if (!value.trim()) {
    store.reset();
    resultsOpen.value = false;
    return;
  }
  resultsOpen.value = true;
  store.search();
}

const { localInput, handleInput, applySearch } = useFilterSearch(
  query,
  runSearch,
);

function close() {
  resultsOpen.value = false;
}

/**
 * Anything on the row that is not the field takes the results down: the row's
 * other controls open layers of their own over the same feed, and two layers
 * over one feed collide.
 */
function onRowPointerDown(event: PointerEvent) {
  const target = event.target as HTMLElement | null;
  if (!target?.closest(".search-container")) close();
}

/** Coming back to the field brings the last hits back with it. */
function reopen() {
  if (hasSearched.value) resultsOpen.value = true;
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === "Escape") {
    close();
    return;
  }
  if (event.key !== "Enter") return;
  event.preventDefault();
  if (localInput.value.trim()) resultsOpen.value = true;
  applySearch();
}

/**
 * Jump to the message. The dropdown deliberately stays up: a search is read
 * hit by hit, and closing it would cost a fresh search per hit.
 */
function jumpTo(result: MessageSearchResult) {
  emit("jump", result.id);
}

/**
 * The page's Ctrl+F lands here: focus the field, restore the last hits, and
 * answer whether there was a field at all — a guest has none, and the page
 * must then leave the browser's own find alone instead of swallowing the key.
 */
function focus(): boolean {
  const field = rootRef.value?.querySelector<HTMLInputElement>("input");
  if (!field) return false;
  field.focus();
  reopen();
  return true;
}

defineExpose({ focus });
</script>

<template>
  <div ref="rootRef" v-click-outside="close" class="chat-search">
    <div class="filter-bar" @pointerdown="onRowPointerDown">
      <FilterSearchInput
        v-if="user"
        v-model="localInput"
        placeholder="Поиск по чату"
        ariaLabel="Поиск по глобальному чату"
        @input="handleInput"
        @keydown="onKeydown"
        @focus="reopen"
        @blur="applySearch"
      />
      <slot />
    </div>

    <div v-if="user && resultsOpen" class="search-dropdown" aria-live="polite">
      <secondary-text v-if="loading" class="search-state">
        Идет поиск...
      </secondary-text>

      <div
        v-else-if="error && results.length === 0"
        class="search-state search-error"
        role="alert"
      >
        <secondary-text>{{ error }}</secondary-text>
        <button type="button" class="retry-button" @click="store.search()">
          Повторить
        </button>
      </div>

      <secondary-text
        v-else-if="hasSearched && results.length === 0"
        class="search-state"
      >
        Ничего не найдено
      </secondary-text>

      <template v-else>
        <button
          v-for="result in results"
          :key="result.id"
          type="button"
          class="result-row"
          @click="jumpTo(result)"
        >
          <span class="result-time">{{
            formatDateFull(result.createdUtc)
          }}</span>
          <span class="result-snippet">
            <template v-for="(part, index) in result.snippet" :key="index">
              <mark v-if="part.isMatch" class="search-highlight">{{
                part.text
              }}</mark>
              <template v-else>{{ part.text }}</template>
            </template>
          </span>
        </button>

        <div v-if="hasMore" class="load-more-row">
          <secondary-text v-if="error" class="load-more-error">
            {{ error }}
          </secondary-text>
          <Button
            type="button"
            :loading="loadingMore"
            @click="store.loadMore()"
          >
            Показать еще
          </Button>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Filters"
@import "@/assets/styles/Inputs"
@import "@/assets/styles/Animations"

// The row IS the site's filter bar, and it is also what the results hang off:
// the chat frame below clips its own overflow, so the layer has to belong to
// something outside it.
.chat-search
  position: relative
  margin-bottom: $small
  +filter-bar
  +filter-responsive

// Results layer — the shared dropdown panel stretched to the full width of the
// row, so it reads as a section over the feed rather than as a menu. Settles in
// on the site's one reveal idiom.
.search-dropdown
  +_dropdown-panel
  left: 0
  right: 0
  animation: search-drop $expand-duration $expand-easing

@keyframes search-drop
  from
    opacity: 0
    transform: translateY(-$small)
  to
    opacity: 1
    transform: translateY(0)

.search-state
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  padding: $medium

.search-error
  color: $accent-red

.retry-button
  +button

// A hit is a control: full width, left aligned, lit on hover, and its text
// stays selectable inside it.
.result-row
  display: flex
  flex-direction: column
  gap: $minor
  width: 100%
  padding: $small $medium
  border: none
  background: transparent
  text-align: left
  cursor: pointer
  user-select: text
  &:hover
    background-color: $hover-overlay
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: -2px

.result-time
  font-size: $secondary-font-size
  color: $text-muted

.result-snippet
  color: $text
  word-break: break-word
  overflow-wrap: break-word

.load-more-row
  display: flex
  flex-direction: column
  align-items: center
  gap: $small
  padding: $medium

.load-more-error
  color: $accent-red
</style>
