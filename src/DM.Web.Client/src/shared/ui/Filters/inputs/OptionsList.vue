<script setup lang="ts">
/**
 * OptionsList - List of selectable options.
 *
 * Used for enum/status filters with optional hierarchical navigation.
 */
import { computed } from "vue";
import { NOTHING_FOUND } from "@/shared/lib/constants/copy";
import { FilterDropdownItem } from "../primitives";
import type { ListOption, OptionsListProps } from "../types";

const props = withDefaults(defineProps<OptionsListProps>(), {
  filterQuery: "",
});

const emit = defineEmits<{
  select: [value: string, hasSubOptions: boolean];
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
      {{ NOTHING_FOUND }}
    </div>
    <FilterDropdownItem
      v-for="option in filteredOptions"
      :key="option.value"
      :label="option.label"
      :hint="option.hint"
      :has-sub-options="option.hasSubOptions"
      @item-select="handleSelect(option)"
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
