<script setup lang="ts">
/**
 * ExpandableBubble - Bubble for multiple values with "и еще N" expansion.
 *
 * Shows first N values, collapses rest into dropdown.
 */
import { ref, computed } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { symbols } from "@/shared/lib/utils/icons";
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
const expandBtnRef = ref<HTMLButtonElement | null>(null);

// Computed values
const sortedValues = computed(() =>
  [...props.values].sort((a, b) =>
    a.label.toLowerCase().localeCompare(b.label.toLowerCase(), "ru"),
  ),
);

const visibleValues = computed(() =>
  sortedValues.value.slice(0, props.maxVisible),
);

const hiddenValues = computed(() => sortedValues.value.slice(props.maxVisible));

const remainingCount = computed(() =>
  Math.max(0, sortedValues.value.length - props.maxVisible),
);

const isExpandable = computed(
  () => sortedValues.value.length > props.maxVisible,
);
const isSingleValue = computed(() => props.values.length === 1);

function toggleDropdown() {
  showDropdown.value = !showDropdown.value;
}

function closeDropdown() {
  showDropdown.value = false;
}

function closeDropdownAndReturnFocus() {
  closeDropdown();
  expandBtnRef.value?.focus();
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
  <!-- Single value - simple bubble. Explicit {{ " " }} space nodes: Vue's
       whitespace condense eats literal spaces, and the copy must read
       "Ведущий: Имя ×" on one line. -->
  <div v-if="isSingleValue" class="bubble bubble-single-owner">
    <span class="bubble-prefix">{{ prefix }}{{ " " }}</span
    ><span class="bubble-owner-item">
      <span class="bubble-value-text">{{ values[0].label }}</span
      ><span class="copy-space">{{ " " }}</span
      ><button
        type="button"
        class="bubble-owner-remove"
        :aria-label="`Убрать фильтр: ${values[0].label}`"
        @click.stop="removeValue(values[0].id)"
      >
        {{ symbols.close }}
      </button>
    </span>
  </div>

  <!-- Multiple values - expandable bubble -->
  <div
    v-else
    class="bubble bubble-expandable"
    :class="{ 'bubble-owners': isExpandable }"
  >
    <span class="bubble-prefix">{{ prefix }}{{ " " }}</span>
    <!-- Visible values -->
    <template v-for="(item, idx) in visibleValues" :key="item.id">
      <span v-if="idx > 0" class="bubble-separator">,{{ " " }}</span>
      <span class="bubble-owner-item">
        <span class="bubble-value-text">{{ item.label }}</span
        ><span class="copy-space">{{ " " }}</span
        ><button
          type="button"
          class="bubble-owner-remove"
          :aria-label="`Убрать фильтр: ${item.label}`"
          @click.stop="removeValue(item.id)"
        >
          {{ symbols.close }}
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
      ref="expandBtnRef"
      type="button"
      class="bubble-expand-btn"
      :class="{ active: showDropdown }"
      aria-haspopup="menu"
      :aria-expanded="showDropdown"
      @click.stop="toggleDropdown"
      @keydown.esc="closeDropdownAndReturnFocus"
    >
      <SvgIcon name="chevronDown" />
    </button>

    <!-- Dropdown for hidden values -->
    <div v-if="showDropdown" class="owners-dropdown" role="menu">
      <button
        v-for="item in hiddenValues"
        :key="item.id"
        type="button"
        class="owners-dropdown-item"
        role="menuitem"
        :aria-label="`Убрать фильтр: ${item.label}`"
        @click.stop="removeFromDropdown(item.id)"
      >
        <span class="owner-name">{{ item.label }}</span>
        <span class="owner-remove">{{ symbols.close }}</span>
      </button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Filters"

+filter-bubbles
</style>
