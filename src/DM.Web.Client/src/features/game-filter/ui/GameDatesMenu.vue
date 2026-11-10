<script setup lang="ts">
import { computed } from "vue";
import type { FilterNavPath } from "@/shared/lib/composables/useFilterDropdown";
import { DateRangePicker, FilterDropdownItem } from "@/shared/ui/Filters";
import { useGamesFilter } from "../model";
import type { GamesFilterKey } from "../model";
import {
  GAME_DATE_RANGES,
  gameDateRange,
  hasRange,
  rangeOf,
} from "../model/gameDateRanges";

const navPath = defineModel<FilterNavPath<GamesFilterKey> | null>("navPath", {
  required: true,
});

const emit = defineEmits<{
  close: [];
}>();

const filter = useGamesFilter();

const range = computed(() => gameDateRange(navPath.value?.group));

const current = computed(() =>
  range.value
    ? rangeOf(filter.filterState.value, range.value)
    : { from: null, to: null },
);

const hasCurrent = computed(
  () => !!range.value && hasRange(filter.filterState.value, range.value),
);

function selectDateType(key: string) {
  navPath.value = { filter: "dates", group: key };
}

function handleDateApply(from: string | null, to: string | null) {
  if (range.value) filter[range.value.setter](from, to);
  emit("close");
}
</script>

<template>
  <template v-if="!navPath?.group">
    <FilterDropdownItem
      v-for="item in GAME_DATE_RANGES"
      :key="item.key"
      :label="item.label"
      :hint="item.hint"
      has-sub-options
      @item-select="selectDateType(item.key)"
    />
  </template>
  <DateRangePicker
    v-else
    :from-value="current.from"
    :to-value="current.to"
    :show-clear-button="hasCurrent"
    @apply="handleDateApply"
    @clear="handleDateApply(null, null)"
  />
</template>
