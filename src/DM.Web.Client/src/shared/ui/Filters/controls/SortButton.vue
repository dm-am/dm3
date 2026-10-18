<script setup lang="ts">
/**
 * SortButton - Sort control with dropdown.
 *
 * Shows current sort option and allows changing sort field/direction.
 *
 * The icon and the name of the sort at every width, as FilterButton. The menu
 * stays inside the window (useKeepInWindow).
 *
 * The menu and the menu of the filters of the same bar are one floating panel
 * at a time: a click on either button is a click past the other, which closes
 * it.
 */
import { ref, computed } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { useMenuKeyboard } from "@/shared/lib/composables/useMenuKeyboard";
import type { SortButtonProps } from "../types";
import { useKeepInWindow } from "./useKeepInWindow";

defineOptions({ name: "SortButton" });

const props = defineProps<SortButtonProps>();

const emit = defineEmits<{
  /**
   * Picking an option carries the direction with it. Consumers whose sort state
   * lands asynchronously (URL-synced filters) would otherwise handle a second,
   * separate direction event while still reading their pre-click direction, and
   * undo the field event they had just been given.
   */
  "sort-select": [sortBy: string, sortOrder?: "asc" | "desc"];
  /** Flipping the direction is its own action, so it stays its own event. */
  "update:sortOrder": [value: "asc" | "desc"];
}>();

// Dropdown state
const showDropdown = ref(false);
const triggerRef = ref<HTMLButtonElement | null>(null);
const dropdownRef = ref<HTMLElement | null>(null);
useKeepInWindow(dropdownRef);

// Current sort label
const currentSortLabel = computed(() => {
  const option = props.options.find((o) => o.value === props.sortBy);
  return option?.label ?? "Сортировка";
});

/** The button's name for a reader of the screen: a sort, and which one is on. */
const buttonName = computed(() => {
  const option = props.options.find((o) => o.value === props.sortBy);
  return option ? `Сортировка: ${option.label}` : "Сортировка";
});

// Sort icon based on direction
const sortIcon = computed(() =>
  props.sortOrder === "asc" ? "sortAsc" : "sortDesc",
);

function toggleDropdown() {
  showDropdown.value = !showDropdown.value;
}

function closeDropdown() {
  showDropdown.value = false;
}

// The role="menu" keyboard contract, shared with the other two menus on the
// site: Escape closes and hands focus back to the trigger, the arrows and
// Home/End walk the options.
const { handleMenuKeydown } = useMenuKeyboard({
  isOpen: () => showDropdown.value,
  close: closeDropdown,
  trigger: triggerRef,
  menu: dropdownRef,
});

function selectSort(value: string) {
  const option = props.options.find((o) => o.value === value);
  // An option that declares no direction leaves the choice to the consumer.
  emit("sort-select", value, option?.defaultDirection);
  closeDropdown();
}

function toggleSortOrder() {
  emit("update:sortOrder", props.sortOrder === "asc" ? "desc" : "asc");
  closeDropdown();
}

// Handle click outside
function handleClickOutside(event: MouseEvent) {
  const target = event.target as HTMLElement;
  if (!target.closest(".sort-section")) {
    closeDropdown();
  }
}

// Setup click outside listener
import { onMounted, onUnmounted } from "vue";

onMounted(() => {
  document.addEventListener("click", handleClickOutside);
});

onUnmounted(() => {
  document.removeEventListener("click", handleClickOutside);
});
</script>

<template>
  <div class="sort-section" @keydown="handleMenuKeydown">
    <button
      ref="triggerRef"
      type="button"
      class="sort-btn"
      :class="{ active: showDropdown }"
      aria-haspopup="menu"
      :aria-expanded="showDropdown"
      :aria-label="buttonName"
      @click="toggleDropdown"
    >
      <SvgIcon :name="sortIcon" class="sort-icon" />
      <span class="sort-btn-label">{{ currentSortLabel }}</span>
    </button>

    <div
      v-if="showDropdown"
      ref="dropdownRef"
      class="sort-dropdown"
      role="menu"
    >
      <!-- Sort options -->
      <button
        v-for="option in options"
        :key="option.value"
        type="button"
        class="sort-option"
        role="menuitem"
        :class="{ selected: option.value === sortBy }"
        @click="selectSort(option.value)"
      >
        <span class="sort-option-content">
          <span class="sort-option-label">{{ option.label }}</span>
          <span v-if="option.hint" class="sort-option-hint">{{
            option.hint
          }}</span>
        </span>
      </button>

      <!-- Divider -->
      <div class="sort-divider" />

      <!-- Direction toggle -->
      <button
        type="button"
        class="sort-option sort-direction"
        role="menuitem"
        @click="toggleSortOrder"
      >
        <SvgIcon :name="sortIcon" class="sort-direction-icon" />
        <span>{{
          sortOrder === "asc" ? "По возрастанию" : "По убыванию"
        }}</span>
      </button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Filters" as *
@use "@/assets/styles/Menu" as *
@use "@/assets/styles/ZIndex" as *

.sort-section
  position: relative
  flex-shrink: 0

.sort-icon
  width: 24px
  height: 24px
  flex-shrink: 0

// The site's menu (_Menu.sass), hanging from the right edge of the button.
.sort-dropdown
  position: absolute
  top: calc(100% + $tiny)
  right: 0
  z-index: $z-dropdown
  min-width: $filter-dropdown-min-width
  +menu-panel

.sort-option
  +menu-item

  &.sort-direction
    display: flex
    align-items: center
    gap: $small

.sort-option-content
  display: flex
  flex-direction: column
  gap: 1px

.sort-option-label
  color: $text

.sort-option-hint
  +menu-hint

.sort-direction-icon
  width: 18px
  height: 18px
  flex-shrink: 0

.sort-divider
  +menu-divider

// Button visual shared with FilterButton through the unified +button mixin —
// guarantees identical colors, hover, active, disabled states.
.sort-btn
  justify-content: flex-start
  gap: $small
  +button
</style>
