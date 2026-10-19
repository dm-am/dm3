<script setup lang="ts">
/**
 * ExpandableBubble - Bubble for multiple values with "и еще N" expansion.
 *
 * Shows first N values, collapses rest into dropdown.
 */
import { ref, computed, onMounted, onUnmounted } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { symbols } from "@/shared/lib/utils/icons";
import { useMenuKeyboard } from "@/shared/lib/composables/useMenuKeyboard";
import type { ExpandableBubbleProps } from "../types";

const props = withDefaults(defineProps<ExpandableBubbleProps>(), {
  maxVisible: 1,
});

const emit = defineEmits<{
  remove: [id: string];
}>();

const showDropdown = ref(false);
const rootRef = ref<HTMLElement | null>(null);
const expandBtnRef = ref<HTMLButtonElement | null>(null);
const dropdownRef = ref<HTMLElement | null>(null);

const sortedValues = computed(() =>
  [...props.values].sort((a, b) =>
    a.label.toLowerCase().localeCompare(b.label.toLowerCase(), "ru"),
  ),
);

const visibleValues = computed(() =>
  sortedValues.value.slice(0, props.maxVisible),
);

const hiddenValues = computed(() => sortedValues.value.slice(props.maxVisible));

const isSingleValue = computed(() => props.values.length === 1);

function toggleDropdown() {
  showDropdown.value = !showDropdown.value;
}

function closeDropdown() {
  showDropdown.value = false;
}

// Escape is heard on the wrapper: the dropdown is the trigger's sibling, not its child.
const { handleMenuKeydown } = useMenuKeyboard({
  isOpen: () => showDropdown.value,
  close: closeDropdown,
  trigger: expandBtnRef,
  menu: dropdownRef,
});

/** Taking a value off closes the list, wherever the value stood. */
function remove(id: string) {
  emit("remove", id);
  closeDropdown();
}

function handleClickOutside(event: MouseEvent) {
  if (!rootRef.value?.contains(event.target as Node)) closeDropdown();
}

onMounted(() => {
  document.addEventListener("click", handleClickOutside);
});

onUnmounted(() => {
  document.removeEventListener("click", handleClickOutside);
});
</script>

<template>
  <!-- Explicit {{ " " }} nodes: Vue condenses literal spaces, and the copy must read "Ведущий: Имя ×". -->
  <div v-if="isSingleValue" class="bubble bubble-single-owner">
    <span class="bubble-prefix">{{ prefix }}{{ " " }}</span
    ><span class="bubble-owner-item">
      <span class="bubble-value-text">{{ values[0].label }}</span
      ><span class="copy-space">{{ " " }}</span
      ><button
        type="button"
        class="bubble-owner-remove"
        :aria-label="`Убрать фильтр: ${values[0].label}`"
        @click.stop="emit('remove', values[0].id)"
      >
        {{ symbols.close }}
      </button>
    </span>
  </div>

  <div
    v-else
    ref="rootRef"
    class="bubble bubble-expandable"
    :class="{ 'bubble-owners': hiddenValues.length > 0 }"
    @keydown="handleMenuKeydown"
  >
    <span class="bubble-prefix">{{ prefix }}{{ " " }}</span>
    <template v-for="(item, idx) in visibleValues" :key="item.id">
      <span v-if="idx > 0" class="bubble-separator">,{{ " " }}</span>
      <span class="bubble-owner-item">
        <span class="bubble-value-text">{{ item.label }}</span
        ><span class="copy-space">{{ " " }}</span
        ><button
          type="button"
          class="bubble-owner-remove"
          :aria-label="`Убрать фильтр: ${item.label}`"
          @click.stop="remove(item.id)"
        >
          {{ symbols.close }}
        </button>
      </span>
    </template>

    <span v-if="hiddenValues.length > 0" class="bubble-remaining">
      и еще {{ hiddenValues.length }}
    </span>

    <button
      v-if="hiddenValues.length > 0"
      ref="expandBtnRef"
      type="button"
      class="bubble-expand-btn"
      :class="{ active: showDropdown }"
      :aria-label="`Показать еще ${hiddenValues.length}`"
      aria-haspopup="menu"
      :aria-expanded="showDropdown"
      @click.stop="toggleDropdown"
    >
      <SvgIcon name="chevronDown" />
    </button>

    <div
      v-if="showDropdown"
      ref="dropdownRef"
      class="owners-dropdown"
      role="menu"
    >
      <button
        v-for="item in hiddenValues"
        :key="item.id"
        type="button"
        class="owners-dropdown-item"
        role="menuitem"
        :aria-label="`Убрать фильтр: ${item.label}`"
        @click.stop="remove(item.id)"
      >
        <span class="owner-name">{{ item.label }}</span>
        <span class="owner-remove">{{ symbols.close }}</span>
      </button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/FilterBubbles" as *

+filter-bubbles
</style>
