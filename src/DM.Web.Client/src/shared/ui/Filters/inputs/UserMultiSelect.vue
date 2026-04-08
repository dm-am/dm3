<script setup lang="ts">
/**
 * UserMultiSelect - Async user search with multi-select.
 *
 * Searches for users via API and allows selecting multiple usernames.
 */
import { ref, watch, computed, onUnmounted } from "vue";
import { FilterDropdownItem } from "../primitives";
import { communityApi } from "@/shared/api";
import type { UserSuggestion } from "../types";

defineOptions({ name: "UserMultiSelect" });

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
  }>(),
  {
    placeholder: "Поиск пользователя",
    maxSuggestions: 6,
    debounceMs: 150,
  },
);

const emit = defineEmits<{
  add: [username: string];
  remove: [username: string];
  keydown: [event: KeyboardEvent];
}>();

// Local state
const searchInput = ref("");
const suggestions = ref<UserSuggestion[]>([]);
const highlightedIndex = ref(-1);
const loading = ref(false);

// Debounce timer
let debounceTimer: ReturnType<typeof setTimeout> | null = null;

// Clean up timer on unmount
onUnmounted(() => {
  if (debounceTimer) {
    clearTimeout(debounceTimer);
  }
});

// Load suggestions when search input changes
watch(searchInput, (query) => {
  if (debounceTimer) {
    clearTimeout(debounceTimer);
  }

  if (!query.trim()) {
    suggestions.value = [];
    highlightedIndex.value = -1;
    loading.value = false;
    return;
  }

  // Set loading immediately to prevent "not found" flash
  loading.value = true;

  debounceTimer = setTimeout(async () => {
    await loadSuggestions(query);
  }, props.debounceMs);
});

async function loadSuggestions(query: string) {
  loading.value = true;
  try {
    const result = await communityApi.searchUsers(query, 10);
    if (!result.data) {
      suggestions.value = [];
      return;
    }
    // Filter out already selected users
    suggestions.value = result.data.resources
      .filter((u) => !props.selectedUsers.has(u.username))
      .slice(0, props.maxSuggestions)
      .map((u) => ({
        username: u.username,
        picture: u.smallPictureUrl,
      }));
    highlightedIndex.value = -1;
  } catch (error) {
    console.error("User search failed:", error);
    suggestions.value = [];
  } finally {
    loading.value = false;
  }
}

// Computed for display
const hasSuggestions = computed(() => suggestions.value.length > 0);
const showEmpty = computed(
  () => searchInput.value.trim() && !loading.value && !hasSuggestions.value,
);

function selectUser(username: string) {
  emit("add", username);
  // Clear search but keep focus for more selections
  searchInput.value = "";
  suggestions.value = [];
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
@import "src/assets/styles/Filters"

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
  text-align: center
</style>
