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
        :aria-expanded="showDropdown && rows.length > 0"
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
        v-if="showDropdown && rows.length > 0"
        :id="listboxId"
        role="listbox"
        class="autocomplete-dropdown"
      >
        <div
          v-for="(row, index) in rows"
          :id="`${listboxId}-option-${index}`"
          :key="row.key"
          role="option"
          :aria-selected="highlightedIndex === index"
          class="autocomplete-item"
          :class="{ highlighted: highlightedIndex === index }"
          @mousedown.prevent="choose(row)"
          @mouseenter="highlightedIndex = index"
        >
          <AvatarImg
            :picture="row.picture"
            :alt="''"
            :size="24"
            img-class="user-avatar"
          />
          <span class="user-username">{{ row.label }}</span>
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
import {
  UserActivityFilter,
  type User,
  type UserPicture,
} from "@/shared/api/models/community";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { vClickOutside } from "@/shared/directives";
import { symbols } from "@/shared/lib/utils/icons";
import { useInDialog } from "@/shared/ui/Layout/dialogContext";
import { useUserSearch } from "../model/useUserSearch";

/** A row offered while nothing is typed, such as a conversation; picking it emits `pick` with its key. */
interface InitialOption {
  key: string;
  label: string;
  picture?: UserPicture | null;
}

/** A row of the list: one of the caller's options, or a person found. */
type Row = InitialOption & { user?: User };

const props = defineProps<{
  modelValue: string;
  placeholder?: string;
  autofocus?: boolean;
  /** Id for the search <input>, so a caller's <label for> reaches it. */
  id?: string;
  /** The names the server would refuse for this field, in any case. */
  exclude?: readonly string[];
  /** A blacklist asks for it: the troll who stays away is the one it is for. */
  includeInactive?: boolean;
  /** Offered in this order while nothing is typed; without it the list waits for a name. */
  initial?: readonly InitialOption[];
  /** The field acts on a choice instead of holding it: the name goes out and the field stays empty. */
  clearOnSelect?: boolean;
}>();

const inDialog = useInDialog();

const inputRef = ref<HTMLInputElement | null>(null);

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
  (e: "pick", key: string): void;
}>();

const SHOWN = 6;

const searchQuery = ref("");
// Twice what is shown, so the names left out still leave a full list.
const { results: suggestions, search } = useUserSearch({
  limit: SHOWN * 2,
  activity: props.includeInactive ? UserActivityFilter.All : undefined,
});

const offered = computed(() => {
  const left = new Set((props.exclude ?? []).map((name) => name.toLowerCase()));
  return suggestions.value
    .filter((user) => !left.has(user.username.toLowerCase()))
    .slice(0, SHOWN);
});

/** What the list draws: the people found once a name is typed, the caller's options before. */
const rows = computed<readonly Row[]>(() =>
  searchQuery.value.trim()
    ? offered.value.map((user) => ({
        key: user.username,
        label: user.username,
        picture: user.picture,
        user,
      }))
    : (props.initial ?? []),
);
const showDropdown = ref(false);
const selectedUser = ref<User | null>(null);
const highlightedIndex = ref(-1);

watch(rows, () => {
  highlightedIndex.value = -1;
});

function onInput() {
  showDropdown.value = true;
  if (selectedUser.value) {
    selectedUser.value = null;
    emit("update:modelValue", "");
  }
  search(searchQuery.value);
}

function choose(row: Row) {
  searchQuery.value = "";
  suggestions.value = [];
  closeDropdown();
  if (!row.user) {
    emit("pick", row.key);
    return;
  }
  if (!props.clearOnSelect) selectedUser.value = row.user;
  emit("update:modelValue", row.user.username);
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
  if (!showDropdown.value || rows.value.length === 0) {
    // Nothing open: Escape goes on to the window the field may stand in.
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
        rows.value.length - 1,
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
        highlightedIndex.value < rows.value.length
      ) {
        choose(rows.value[highlightedIndex.value]);
      }
      break;
    case "Escape":
      // The open list takes it, so the window the field stands in stays.
      event.preventDefault();
      event.stopPropagation();
      showDropdown.value = false;
      highlightedIndex.value = -1;
      break;
  }
}

defineExpose({ focus: () => inputRef.value?.focus() });

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
@use "@/assets/styles/Filters" as *
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
  max-height: $filter-dropdown-max-height
  overflow-y: auto
  border-radius: $border-radius
  background: $bg-element
  border: 1px solid $border
  box-shadow: $shadow-panel

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
  width: $filter-avatar-size
  height: $filter-avatar-size
  margin-right: $small

.user-username
  color: $text

// The gap keeps the cross off the last letter where the field is as wide as its content.
.selected-user
  +input-base()
  display: flex
  align-items: center
  gap: $small
  box-sizing: border-box

  .user-avatar
    margin-right: 0

// In a window, the height and size of the window's other fields.
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
