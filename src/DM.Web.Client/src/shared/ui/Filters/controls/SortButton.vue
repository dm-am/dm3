<script setup lang="ts">
/**
 * SortButton - Sort control with dropdown.
 *
 * Shows current sort option and allows changing sort field/direction.
 */
import { ref, computed } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";
import type { SortOption } from "../types";

defineOptions({ name: "SortButton" });

const props = defineProps<{
  /** Available sort options */
  options: SortOption[];
  /** Current sort field */
  sortBy: string;
  /** Current sort direction */
  sortOrder: "asc" | "desc";
}>();

const emit = defineEmits<{
  "update:sortBy": [value: string];
  "update:sortOrder": [value: "asc" | "desc"];
}>();

// Dropdown state
const showDropdown = ref(false);

// Current sort label
const currentSortLabel = computed(() => {
  const option = props.options.find((o) => o.value === props.sortBy);
  return option?.label ?? "Сортировка";
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

function selectSort(value: string) {
  const option = props.options.find((o) => o.value === value);
  emit("update:sortBy", value);
  // Apply default direction if defined
  if (option?.defaultDirection) {
    emit("update:sortOrder", option.defaultDirection);
  }
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
  <div class="sort-section">
    <button
      type="button"
      class="sort-btn"
      :class="{ active: showDropdown }"
      @click="toggleDropdown"
    >
      <SvgIcon :name="sortIcon" class="sort-icon" />
      <span>{{ currentSortLabel }}</span>
    </button>

    <div v-if="showDropdown" class="sort-dropdown">
      <!-- Sort options -->
      <button
        v-for="option in options"
        :key="option.value"
        type="button"
        class="sort-option"
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
@import "src/assets/styles/Inputs"
@import "src/assets/styles/Filters"

+sort-control

// Button visual shared with FilterButton through the unified +button mixin —
// guarantees identical colors, hover, active, disabled states.
.sort-btn
  justify-content: flex-start
  gap: $small
  +button
</style>
