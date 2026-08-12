<template>
  <div v-click-outside="closeDropdown" class="user-autocomplete">
    <template v-if="!selectedUser">
      <input
        :id="id"
        ref="inputRef"
        v-model="searchQuery"
        type="text"
        class="autocomplete-input"
        :placeholder="placeholder"
        :aria-label="placeholder || 'Поиск пользователя'"
        role="combobox"
        :aria-expanded="showDropdown && suggestions.length > 0"
        aria-autocomplete="list"
        :aria-controls="listboxId"
        :aria-activedescendant="
          highlightedIndex >= 0
            ? `${listboxId}-option-${highlightedIndex}`
            : undefined
        "
        @input="onInput"
        @focus="showDropdown = true"
        @keydown="onKeydown"
      />
      <div
        v-if="showDropdown && suggestions.length > 0"
        :id="listboxId"
        role="listbox"
        class="autocomplete-dropdown"
      >
        <div
          v-for="(user, index) in suggestions"
          :id="`${listboxId}-option-${index}`"
          :key="user.username"
          role="option"
          :aria-selected="highlightedIndex === index"
          class="autocomplete-item"
          :class="{ highlighted: highlightedIndex === index }"
          @mousedown.prevent="selectUser(user)"
          @mouseenter="highlightedIndex = index"
        >
          <AvatarImg
            :picture="user.picture"
            :alt="''"
            :size="24"
            img-class="user-avatar"
          />
          <span class="user-username">{{ user.username }}</span>
        </div>
      </div>
    </template>
    <div v-else class="selected-user">
      <AvatarImg
        :picture="selectedUser.picture"
        :alt="''"
        :size="24"
        img-class="user-avatar"
      />
      <span class="user-username">{{ selectedUser.username }}</span>
      <button
        type="button"
        class="clear-btn"
        aria-label="Очистить выбор"
        @click="clearSelection"
      >
        {{ symbols.close }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, nextTick } from "vue";
import type { User } from "@/shared/api/models/community";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { vClickOutside } from "@/shared/directives";
import { symbols } from "@/shared/lib/utils/icons";
import { useUserSearch } from "../model/useUserSearch";

const props = defineProps<{
  modelValue: string;
  placeholder?: string;
  autofocus?: boolean;
  /** Id for the search <input>, so a caller's <label for> reaches it. */
  id?: string;
}>();

const inputRef = ref<HTMLInputElement | null>(null);

// Generate unique ID for ARIA
const listboxId = `autocomplete-listbox-${Math.random().toString(36).slice(2, 9)}`;

onMounted(() => {
  if (props.autofocus) {
    nextTick(() => {
      inputRef.value?.focus();
    });
  }
});

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
}>();

const searchQuery = ref("");
// Shared debounced lookup; `suggestions` is the raw User[] result.
const { results: suggestions, search } = useUserSearch({
  limit: 6,
  debounceMs: 150,
});
const showDropdown = ref(false);
const selectedUser = ref<User | null>(null);
const highlightedIndex = ref(-1);

// A fresh result set invalidates the keyboard highlight.
watch(suggestions, () => {
  highlightedIndex.value = -1;
});

function onInput() {
  if (selectedUser.value) {
    selectedUser.value = null;
    emit("update:modelValue", "");
  }
  search(searchQuery.value);
}

function selectUser(user: User) {
  selectedUser.value = user;
  searchQuery.value = "";
  suggestions.value = [];
  showDropdown.value = false;
  highlightedIndex.value = -1;
  emit("update:modelValue", user.username);
}

function clearSelection() {
  selectedUser.value = null;
  searchQuery.value = "";
  highlightedIndex.value = -1;
  emit("update:modelValue", "");
  nextTick(() => {
    inputRef.value?.focus();
  });
}

function closeDropdown() {
  showDropdown.value = false;
  highlightedIndex.value = -1;
}

function onKeydown(event: KeyboardEvent) {
  if (!showDropdown.value || suggestions.value.length === 0) {
    if (event.key === "Escape") {
      showDropdown.value = false;
    }
    return;
  }

  switch (event.key) {
    case "ArrowDown":
      event.preventDefault();
      highlightedIndex.value = Math.min(
        highlightedIndex.value + 1,
        suggestions.value.length - 1,
      );
      break;
    case "ArrowUp":
      event.preventDefault();
      highlightedIndex.value = Math.max(highlightedIndex.value - 1, 0);
      break;
    case "Enter":
      event.preventDefault();
      if (
        highlightedIndex.value >= 0 &&
        highlightedIndex.value < suggestions.value.length
      ) {
        selectUser(suggestions.value[highlightedIndex.value]);
      }
      break;
    case "Escape":
      event.preventDefault();
      showDropdown.value = false;
      highlightedIndex.value = -1;
      break;
  }
}

watch(
  () => props.modelValue,
  (newVal) => {
    if (!newVal) {
      selectedUser.value = null;
      searchQuery.value = "";
    }
  },
);
</script>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "@/assets/styles/ZIndex"

.user-autocomplete
  position: relative

.autocomplete-input
  +input()
  &
    width: 100%
    box-sizing: border-box

.autocomplete-dropdown
  position: absolute
  top: calc(100% + $tiny)
  left: 0
  right: 0
  z-index: $z-dropdown
  max-height: 300px
  overflow-y: auto
  border-radius: $border-radius
  background: $bg-element
  border: 1px solid $border
  box-shadow: 0 4px 12px $shadow-color

.autocomplete-item
  display: flex
  align-items: center
  padding: $small $medium
  cursor: pointer
  border-bottom: 1px solid $border

  &:hover,
  &.highlighted
    background: $bg-element-accent

  &:last-child
    border-bottom: none

.user-avatar
  width: 24px
  height: 24px
  margin-right: $small

.user-username
  color: $text

.selected-user
  +input-base()
  display: flex
  align-items: center
  box-sizing: border-box

.clear-btn
  margin-left: auto
  padding: 0
  border: none
  background: none
  font-size: 1.2em
  cursor: pointer
  color: $text-muted
  &:hover
    color: $text
</style>
