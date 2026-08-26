<script setup lang="ts">
/**
 * UserMultiSelect - Async user search with multi-select.
 *
 * Searches for users via API and allows selecting multiple usernames.
 */
import { ref, watch, computed } from "vue";
import { FilterDropdownItem } from "@/shared/ui/Filters";
import { UserActivityFilter } from "@/shared/api/models/community";
import { useUserSearch } from "../model/useUserSearch";

defineOptions({ name: "UserMultiSelect" });

/** A shaped user suggestion for the dropdown. */
interface UserSuggestion {
  username: string;
  picture?: string;
}

const props = withDefaults(
  defineProps<{
    /** Currently selected usernames */
    selectedUsers: Set<string>;
    /** Placeholder text for search input */
    placeholder?: string;
    /** Maximum suggestions to show */
    maxSuggestions?: number;
    /** Debounce delay for search */
    debounceMs?: number;
    /**
     * Include users inactive for 30+ days in search results
     * (activity=All). Default keeps the backend's Active-only filter.
     */
    includeInactive?: boolean;
  }>(),
  {
    placeholder: "Поиск пользователя",
    maxSuggestions: 6,
    debounceMs: 150,
    includeInactive: false,
  },
);

const emit = defineEmits<{
  add: [username: string];
  remove: [username: string];
  keydown: [event: KeyboardEvent];
}>();

// Local state
const searchInput = ref("");
const highlightedIndex = ref(-1);

// Shared debounced lookup. `activity` stays undefined unless inactive users
// are requested, so the two-arg request keeps the backend's Active-only default.
const { results, loading, search, clear } = useUserSearch({
  limit: 10,
  debounceMs: props.debounceMs,
  activity: props.includeInactive ? UserActivityFilter.All : undefined,
});

// Shape the raw results: drop already-selected users, cap to maxSuggestions,
// map to the dropdown's {username, picture} shape.
const suggestions = computed<UserSuggestion[]>(() =>
  results.value
    .filter((u) => !props.selectedUsers.has(u.username))
    .slice(0, props.maxSuggestions)
    .map((u) => ({
      username: u.username,
      picture: u.picture?.smallUrl,
    })),
);

watch(searchInput, (query) => {
  highlightedIndex.value = -1;
  search(query);
});

// Computed for display
const hasSuggestions = computed(() => suggestions.value.length > 0);
const showEmpty = computed(
  () => searchInput.value.trim() && !loading.value && !hasSuggestions.value,
);

function selectUser(username: string) {
  emit("add", username);
  // Clear search but keep focus for more selections. `clear()` drops the raw
  // results (and any pending request) synchronously — `suggestions` is derived
  // from them, and the searchInput watcher only runs on the next tick, so
  // without it the just-picked list would linger for a frame.
  searchInput.value = "";
  clear();
  highlightedIndex.value = -1;
}

function handleInputChange(event: Event) {
  searchInput.value = (event.target as HTMLInputElement).value;
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === "ArrowDown") {
    event.preventDefault();
    if (highlightedIndex.value < suggestions.value.length - 1) {
      highlightedIndex.value++;
    }
    return;
  }

  if (event.key === "ArrowUp") {
    event.preventDefault();
    if (highlightedIndex.value > 0) {
      highlightedIndex.value--;
    }
    return;
  }

  if (event.key === "Enter" && highlightedIndex.value >= 0) {
    event.preventDefault();
    const suggestion = suggestions.value[highlightedIndex.value];
    if (suggestion) {
      selectUser(suggestion.username);
    }
    return;
  }

  // Pass other keys to parent
  emit("keydown", event);
}
</script>

<template>
  <div class="user-multi-select">
    <div class="dropdown-search">
      <input
        :value="searchInput"
        type="text"
        class="dropdown-search-input"
        :placeholder="placeholder"
        :aria-label="placeholder"
        @input="handleInputChange"
        @keydown="handleKeydown"
      />
    </div>

    <div v-if="showEmpty" class="dropdown-empty">Пользователь не найден</div>

    <template v-else-if="hasSuggestions">
      <FilterDropdownItem
        v-for="(suggestion, index) in suggestions"
        :key="suggestion.username"
        :label="suggestion.username"
        :avatar-url="suggestion.picture"
        :highlighted="index === highlightedIndex"
        @item-select="selectUser(suggestion.username)"
        @mouseenter="highlightedIndex = index"
      />
    </template>

    <div v-else-if="!searchInput.trim()" class="dropdown-empty">
      Введите имя пользователя
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *

// Use standard mixins for consistent styling
+dropdown-search-input

.user-multi-select
  max-height: 300px
  overflow-y: auto

// Explicit empty state styling (mixin may not apply correctly in scoped context)
.dropdown-empty
  padding: $medium
  font-size: $secondary-font-size
  color: $text-muted
</style>
