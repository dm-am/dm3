<script setup lang="ts">
import { ref, watch, computed } from "vue";
import { FilterDropdownItem } from "@/shared/ui/Filters";
import { UserActivityFilter } from "@/shared/api/models/community";
import { useUserSearch } from "../model/useUserSearch";

defineOptions({ name: "UserMultiSelect" });

interface UserSuggestion {
  username: string;
  picture?: string;
}

const props = withDefaults(
  defineProps<{
    selectedUsers: Set<string>;
    placeholder?: string;
    /** The users away for thirty days and more too. */
    includeInactive?: boolean;
  }>(),
  {
    placeholder: "Поиск пользователя",
    includeInactive: false,
  },
);

const emit = defineEmits<{
  add: [username: string];
  keydown: [event: KeyboardEvent];
}>();

const SHOWN = 6;

const searchInput = ref("");
const highlightedIndex = ref(-1);

const { results, loading, search, clear } = useUserSearch({
  limit: 10,
  debounceMs: 150,
  activity: props.includeInactive ? UserActivityFilter.All : undefined,
});

const suggestions = computed<UserSuggestion[]>(() =>
  results.value
    .filter((u) => !props.selectedUsers.has(u.username))
    .slice(0, SHOWN)
    .map((u) => ({
      username: u.username,
      picture: u.picture?.smallUrl,
    })),
);

watch(searchInput, (query) => {
  highlightedIndex.value = -1;
  search(query);
});

const hasSuggestions = computed(() => suggestions.value.length > 0);
const showEmpty = computed(
  () => searchInput.value.trim() && !loading.value && !hasSuggestions.value,
);

function selectUser(username: string) {
  emit("add", username);
  // clear() at once: the watcher runs only on the next tick, and the list would linger.
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

+dropdown-search-input

.user-multi-select
  max-height: $filter-dropdown-max-height
  overflow-y: auto

.dropdown-empty
  padding: $medium
  font-size: $secondary-font-size
  color: $text-muted
</style>
