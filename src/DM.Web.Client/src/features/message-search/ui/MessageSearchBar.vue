<script setup lang="ts">
// Stands above the chat frame; picking a hit jumps the feed and leaves the dropdown open.
// A guest gets the row without the field: the endpoint is authenticated.
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { vClickOutside } from "@/shared/directives";
import { FilterSearchInput } from "@/shared/ui/Filters";
import { useFilterSearch } from "@/shared/lib/composables/useFilterSearch";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { NOTHING_FOUND } from "@/shared/lib/constants/copy";
import { useAuthStore } from "@/shared/stores";
import { useMessageSearchStore } from "../model/searchStore";
import type { MessageSearchResult, MessageSearchScope } from "../model/types";

const props = withDefaults(
  defineProps<{
    scope?: MessageSearchScope;
  }>(),
  { scope: "global" },
);

const emit = defineEmits<{
  jump: [messageId: string];
}>();

const store = useMessageSearchStore();
const { query, results, hasMore, loading, loadingMore, error, hasSearched } =
  storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

// Before the field reads the query back on mount: another feed's query is not this one's.
store.setScope(props.scope);

const words = computed(() =>
  props.scope === "global"
    ? { placeholder: "Поиск по чату", label: "Поиск по глобальному чату" }
    : { placeholder: "Поиск по переписке", label: "Поиск по переписке" },
);

const rootRef = ref<HTMLElement | null>(null);

const resultsOpen = ref(false);

watch(
  () => props.scope,
  (scope) => {
    store.setScope(scope);
    resultsOpen.value = false;
  },
);

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

// Any other control of the row opens a layer over the same feed, so the results go down.
function onRowPointerDown(event: PointerEvent) {
  const target = event.target as HTMLElement | null;
  if (!target?.closest(".search-container")) close();
}

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

// The dropdown stays up: a search is read hit by hit.
function jumpTo(result: MessageSearchResult) {
  emit("jump", result.id);
}

// Answers whether there was a field: a guest has none, and the browser's own find stays.
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
        :placeholder="words.placeholder"
        :ariaLabel="words.label"
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
        {{ NOTHING_FOUND }}
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
@use "@/assets/styles/Filters" as *
@use "@/assets/styles/Buttons" as *
@use "@/assets/styles/Animations" as *

// The chat frame clips its overflow, so the results layer belongs to the row.
.chat-search
  position: relative
  margin-bottom: $small
  +filter-bar

.search-dropdown
  +dropdown-panel
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
  // Hits stand flush, so the ring goes inward.
  &:focus-visible
    +focus-ring($inward: true)

.result-time
  font-size: $secondary-font-size
  color: $text-muted

.result-snippet
  color: $text
  overflow-wrap: anywhere

.load-more-row
  display: flex
  flex-direction: column
  align-items: center
  gap: $small
  padding: $medium

.load-more-error
  color: $accent-red
</style>
