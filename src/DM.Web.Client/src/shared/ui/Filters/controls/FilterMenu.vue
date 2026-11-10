<script setup lang="ts">
/**
 * FilterMenu - The "Фильтры" button and its dropdown: the root lists the
 * filters, a deeper level shows its title with a way back above the default
 * slot. Open state and navigation stay with the parent (useFilterDropdown).
 */
import { vClickOutside } from "@/shared/directives";
import type { FilterNavPath } from "@/shared/lib/composables/useFilterDropdown";
import { FilterDropdownHeader, FilterDropdownItem } from "../primitives";
import FilterButton from "./FilterButton.vue";
import FilterDropdown from "./FilterDropdown.vue";

interface FilterMenuOption {
  key: string;
  label: string;
  hint?: string;
}

defineProps<{
  open: boolean;
  /** Header of a level below the root. */
  title: string | null;
  options: readonly FilterMenuOption[];
}>();

const navPath = defineModel<FilterNavPath | null>("navPath", {
  required: true,
});

const emit = defineEmits<{
  toggle: [];
  close: [];
  back: [];
}>();
</script>

<template>
  <div v-click-outside="() => emit('close')" class="filter-section">
    <FilterButton
      :active="open"
      @click="emit('toggle')"
      @close="emit('close')"
    />

    <FilterDropdown v-if="open" @close="emit('close')">
      <FilterDropdownHeader
        v-if="navPath"
        :title="title ?? ''"
        @back="emit('back')"
      />

      <template v-if="!navPath">
        <FilterDropdownItem
          v-for="item in options"
          :key="item.key"
          :label="item.label"
          :hint="item.hint"
          has-sub-options
          @item-select="navPath = { filter: item.key }"
        />
      </template>

      <slot />
    </FilterDropdown>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *

+filter-button
</style>
