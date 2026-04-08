<script setup lang="ts">
/**
 * ExpandableBubble - Bubble for multiple values with "и еще N" expansion.
 *
 * Shows first N values, collapses rest into dropdown.
 */
import { ref, computed } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";
import type { BubbleValue } from "../types";

defineOptions({ name: "ExpandableBubble" });

const props = withDefaults(
  defineProps<{
    /** Prefix text (e.g., "Авторы:") */
    prefix: string;
    /** All values */
    values: BubbleValue[];
    /** Maximum visible values before collapse */
    maxVisible?: number;
  }>(),
  {
    maxVisible: 1,
  },
);

const emit = defineEmits<{
  remove: [id: string];
}>();

// Dropdown state
const showDropdown = ref(false);

// Computed values
const sortedValues = computed(() =>
  [...props.values].sort((a, b) =>
    a.label.toLowerCase().localeCompare(b.label.toLowerCase(), "ru"),
  ),
);

const visibleValues = computed(() =>
  sortedValues.value.slice(0, props.maxVisible),
);

const hiddenValues = computed(() =>
  sortedValues.value.slice(props.maxVisible),
);

const remainingCount = computed(() =>
  Math.max(0, sortedValues.value.length - props.maxVisible),
);

const isExpandable = computed(() => sortedValues.value.length > props.maxVisible);
const isSingleValue = computed(() => props.values.length === 1);

function toggleDropdown() {
  showDropdown.value = !showDropdown.value;
}

function closeDropdown() {
  showDropdown.value = false;
}

function removeValue(id: string) {
  emit("remove", id);
}

function removeFromDropdown(id: string) {
  emit("remove", id);
  // Close dropdown if no more hidden values
  if (hiddenValues.value.length <= 1) {
    closeDropdown();
  }
}

// Handle click outside
function handleClickOutside(event: MouseEvent) {
  const target = event.target as HTMLElement;
  if (!target.closest(".bubble-expandable")) {
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
  <!-- Single value - simple bubble -->
  <div
    v-if="isSingleValue"
    class="bubble bubble-single-owner"
  >
    <span class="bubble-prefix">{{ prefix }} </span>
    <span class="bubble-owner-item">
      <span class="bubble-value-text">{{ values[0].label }}</span>
      <button
        type="button"
        class="bubble-owner-remove"
        @click.stop="removeValue(values[0].id)"
      >
        <SvgIcon name="closeThin" />
      </button>
    </span>
  </div>

  <!-- Multiple values - expandable bubble -->
  <div v-else class="bubble bubble-expandable" :class="{ 'bubble-owners': isExpandable }">
    <span class="bubble-prefix">{{ prefix }} </span>

    <!-- Visible values -->
    <template v-for="(item, idx) in visibleValues" :key="item.id">
      <span v-if="idx > 0" class="bubble-separator">,</span>
      <span class="bubble-owner-item">
        <span class="bubble-value-text">{{ item.label }}</span>
        <button
          type="button"
          class="bubble-owner-remove"
          @click.stop="removeValue(item.id)"
        >
          <SvgIcon name="closeThin" />
        </button>
      </span>
    </template>

    <!-- Remaining count -->
    <span v-if="remainingCount > 0" class="bubble-remaining">
      и еще {{ remainingCount }}
    </span>

    <!-- Expand button -->
    <button
      v-if="isExpandable"
      type="button"
      class="bubble-expand-btn"
      :class="{ active: showDropdown }"
      @click.stop="toggleDropdown"
    >
      <SvgIcon name="expandDown" />
    </button>

    <!-- Dropdown for hidden values -->
    <div v-if="showDropdown" class="owners-dropdown">
      <button
        v-for="item in hiddenValues"
        :key="item.id"
        type="button"
        class="owners-dropdown-item"
        @click.stop="removeFromDropdown(item.id)"
      >
        <span class="owner-name">{{ item.label }}</span>
        <SvgIcon name="closeThin" class="owner-remove" />
      </button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Filters"

+filter-bubbles
</style>
