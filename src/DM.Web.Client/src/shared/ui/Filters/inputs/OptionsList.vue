<script setup lang="ts">
/**
 * OptionsList - List of selectable options.
 *
 * Used for enum/status filters with optional hierarchical navigation.
 */
import { computed } from "vue";
import { FilterDropdownItem } from "../primitives";
import type { ListOption, OptionsListProps } from "../types";

defineOptions({ name: "OptionsList" });

const props = withDefaults(defineProps<OptionsListProps>(), {
  highlightedIndex: -1,
  filterQuery: "",
});

const emit = defineEmits<{
  select: [value: string, hasSubOptions: boolean];
  highlight: [index: number];
}>();

// Filter options by query
const filteredOptions = computed(() => {
  if (!props.filterQuery) return props.options;
  const query = props.filterQuery.toLowerCase();
  return props.options.filter(
    (opt) =>
      opt.label.toLowerCase().includes(query) ||
      opt.hint?.toLowerCase().includes(query),
  );
});

function handleSelect(option: ListOption) {
  emit("select", option.value, option.hasSubOptions ?? false);
}
</script>

<template>
  <div class="options-list">
    <div v-if="filteredOptions.length === 0" class="dropdown-empty">
      Ничего не найдено
    </div>
    <FilterDropdownItem
      v-for="(option, index) in filteredOptions"
      :key="option.value"
      :label="option.label"
      :hint="option.hint"
      :highlighted="index === highlightedIndex"
      :has-sub-options="option.hasSubOptions"
      @item-select="handleSelect(option)"
      @mouseenter="emit('highlight', index)"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *

.options-list
  max-height: 250px
  overflow-y: auto

.dropdown-empty
  padding: $medium
  font-size: $secondary-font-size
  color: $text-muted
</style>
