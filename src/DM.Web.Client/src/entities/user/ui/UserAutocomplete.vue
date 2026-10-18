<template>
  <div
    v-click-outside="closeDropdown"
    class="user-autocomplete"
    :class="{ 'user-autocomplete--dialog': inDialog }"
  >
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
        :aria-expanded="showDropdown && offered.length > 0"
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
        v-if="showDropdown && offered.length > 0"
        :id="listboxId"
        role="listbox"
        class="autocomplete-dropdown"
      >
        <div
          v-for="(user, index) in offered"
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
import { computed, ref, watch, onMounted, nextTick } from "vue";
import { UserActivityFilter, type User } from "@/shared/api/models/community";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { vClickOutside } from "@/shared/directives";
import { symbols } from "@/shared/lib/utils/icons";
import { useInDialog } from "@/shared/ui/Layout/dialogContext";
import { useUserSearch } from "../model/useUserSearch";

const props = defineProps<{
  modelValue: string;
  placeholder?: string;
  autofocus?: boolean;
  /** Id for the search <input>, so a caller's <label for> reaches it. */
  id?: string;
  /**
   * Usernames the list does not offer, in any case: the ones the server would
   * refuse for this field (the viewer, the names already on a blacklist, the
   * people who run the game).
   */
  exclude?: readonly string[];
  /**
   * Look among everyone, the people who have not come by for a month too
   * (activity=All), as UserMultiSelect does. Left off, the request keeps its
   * two arguments and the server its Active-only default. A blacklist asks
   * for it: the troll who stays away is the one it is for.
   */
  includeInactive?: boolean;
}>();

/** A field in a window takes the window's height and size (FormField). */
const inDialog = useInDialog();

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

/** How many names the list offers at most. */
const SHOWN = 6;

const searchQuery = ref("");
// Shared debounced lookup; `suggestions` is the raw User[] result. Twice what
// is shown is asked for, so the names left out still leave a full list.
const { results: suggestions, search } = useUserSearch({
  limit: SHOWN * 2,
  debounceMs: 150,
  activity: props.includeInactive ? UserActivityFilter.All : undefined,
});

/** What the list offers: the lookup less `exclude`, the first SHOWN. */
const offered = computed(() => {
  const left = new Set((props.exclude ?? []).map((name) => name.toLowerCase()));
  return suggestions.value
    .filter((user) => !left.has(user.username.toLowerCase()))
    .slice(0, SHOWN);
});
const showDropdown = ref(false);
const selectedUser = ref<User | null>(null);
const highlightedIndex = ref(-1);

// A fresh result set invalidates the keyboard highlight.
watch(offered, () => {
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
  if (!showDropdown.value || offered.value.length === 0) {
    // Nothing open to close: Escape is left to go on, to the window it may
    // stand in.
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
        offered.value.length - 1,
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
        highlightedIndex.value < offered.value.length
      ) {
        selectUser(offered.value[highlightedIndex.value]);
      }
      break;
    case "Escape":
      // The open list takes this Escape: it closes the list, and the window
      // the field stands in stays (Dialog reads an Escape that reaches it).
      event.preventDefault();
      event.stopPropagation();
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
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/DialogSurface" as *

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

// A chosen name: the picture, the name and the cross, a step of 8 apart at
// least. Where the field is wider than the name, the cross stands at its right
// edge (margin-left auto) and the gap only takes from that space; where the
// field is as wide as what it holds (the blacklists, BlacklistAddRow), the gap
// is what keeps the cross off the last letter, which auto alone did not.
.selected-user
  +input-base()
  display: flex
  align-items: center
  gap: $small
  box-sizing: border-box

  // The gap stands between the picture and the name, where its margin did.
  .user-avatar
    margin-right: 0

// In a window the field is the window's: the height and the size of its other
// fields (FormField), the chosen name at the same height as the typed one.
.user-autocomplete--dialog
  .autocomplete-input,
  .selected-user
    height: $dialog-control-height
    padding: 0 $dialog-field-inset
    font-size: $dialog-field-font-size

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
