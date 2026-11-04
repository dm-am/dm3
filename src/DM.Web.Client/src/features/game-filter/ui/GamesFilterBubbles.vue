<script setup lang="ts">
// Backspace in the empty search takes the bubbles off from the end of this row.
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useGamesStore, type Tag } from "@/entities/game";
import { formatDateRangeForDisplay } from "@/shared/lib/filters";
import {
  BubblesRow,
  ExpandableBubble,
  FilterBubble,
} from "@/shared/ui/Filters";
import {
  useGamesFilter,
  STATUS_OPTIONS,
  RECRUITMENT_FILTER_OPTIONS,
  CLOSED_REASON_FILTER_OPTIONS,
} from "../model";
import { GAME_DATE_RANGES, hasRange, rangeOf } from "../model/gameDateRanges";
import { tagBubbles } from "../model/gameTags";

const emit = defineEmits<{
  "clear-all": [];
}>();

const filter = useGamesFilter();
const { filterState, clearStatus, removeTag, removeHost } = filter;

const { tags } = storeToRefs(useGamesStore());

const tagMap = computed(() => {
  const map: Record<number, Tag> = {};
  for (const tag of tags.value ?? []) map[tag.id] = tag;
  return map;
});

const statusBubbleLabel = computed(() => {
  const status = filterState.value.status;
  if (!status) return "";

  const label = STATUS_OPTIONS.find((o) => o.value === status)?.label ?? status;
  let subLabel = "";

  if (status === "Active" && filterState.value.recruitmentFilter !== "any") {
    const opt = RECRUITMENT_FILTER_OPTIONS.find(
      (o) => o.value === filterState.value.recruitmentFilter,
    );
    subLabel = opt?.label.toLowerCase() ?? "";
  }
  if (status === "Closed" && filterState.value.closedReasonFilter !== "any") {
    const opt = CLOSED_REASON_FILTER_OPTIONS.find(
      (o) => o.value === filterState.value.closedReasonFilter,
    );
    subLabel = opt?.label.toLowerCase() ?? "";
  }

  return subLabel ? `${label} (${subLabel})` : label;
});

const dateBubbles = computed(() =>
  GAME_DATE_RANGES.filter((range) => hasRange(filterState.value, range)).map(
    (range) => ({
      range,
      label: formatDateRangeForDisplay(rangeOf(filterState.value, range)),
    }),
  ),
);

const requiredTagsBubbles = computed(() =>
  tagBubbles(filterState.value.requiredTags, tagMap.value),
);

const excludedTagsBubbles = computed(() =>
  tagBubbles(filterState.value.excludedTags, tagMap.value),
);

const hostsBubbleValues = computed(() =>
  [...filterState.value.hostUsernames]
    .sort((a, b) => a.toLowerCase().localeCompare(b.toLowerCase(), "ru"))
    .map((username) => ({ id: username, label: username })),
);

// A non-default sort alone must not show a row with a lone "Сбросить".
const hasBubbles = computed(() => {
  const state = filterState.value;
  return (
    state.status !== null ||
    state.hostUsernames.size > 0 ||
    dateBubbles.value.length > 0 ||
    state.requiredTags.size > 0 ||
    state.excludedTags.size > 0
  );
});
</script>

<template>
  <BubblesRow v-if="hasBubbles" @clear-all="emit('clear-all')">
    <FilterBubble
      v-if="filterState.status !== null"
      prefix="Статус игры:"
      :value="statusBubbleLabel"
      @remove="clearStatus"
    />

    <ExpandableBubble
      v-if="filterState.hostUsernames.size > 0"
      :prefix="filterState.hostUsernames.size > 1 ? 'Ведущие:' : 'Ведущий:'"
      :values="hostsBubbleValues"
      :max-visible="1"
      @remove="removeHost"
    />

    <FilterBubble
      v-for="date in dateBubbles"
      :key="date.range.key"
      :prefix="date.range.bubble"
      :value="date.label"
      @remove="filter[date.range.setter](null, null)"
    />

    <FilterBubble
      v-for="tag in requiredTagsBubbles"
      :key="`required-${tag.tagId}`"
      prefix="Тег:"
      :value="tag.title"
      @remove="removeTag(tag.tagId)"
    />

    <FilterBubble
      v-for="tag in excludedTagsBubbles"
      :key="`excluded-${tag.tagId}`"
      prefix="Без тега:"
      :value="tag.title"
      @remove="removeTag(tag.tagId)"
    />
  </BubblesRow>
</template>
