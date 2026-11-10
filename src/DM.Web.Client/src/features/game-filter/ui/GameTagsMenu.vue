<script setup lang="ts">
// The search is the menu's own: emptied on close, on the step back and on a group change.
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useGamesStore } from "@/entities/game";
import type { FilterNavPath } from "@/shared/lib/composables/useFilterDropdown";
import { FilterDropdownItem } from "@/shared/ui/Filters";
import { NOTHING_FOUND } from "@/shared/lib/constants/copy";
import { useGamesFilter } from "../model";
import type { GamesFilterKey } from "../model";
import { byTagOrder, descriptionText } from "../model/gameTags";

const navPath = defineModel<FilterNavPath<GamesFilterKey> | null>("navPath", {
  required: true,
});

const { filterState, addRequiredTag, addExcludedTag } = useGamesFilter();

// The cached store: one request per page, shared with the table and the tag cloud.
const gamesStore = useGamesStore();
const { tags: storeTags, tagsError } = storeToRefs(gamesStore);
const tags = computed(() => storeTags.value ?? []);
// "Nothing found" only once the tags have come: an empty list before the answer is not an empty answer.
const tagsCame = computed(() => storeTags.value !== null);
const tagSearchQuery = ref("");

watch(
  () => navPath.value?.group,
  () => {
    tagSearchQuery.value = "";
  },
);

const tagGroups = computed(() => {
  const groupSortMap = new Map<string, number>();
  for (const tag of tags.value) {
    if (tag.groupTitle && !groupSortMap.has(tag.groupTitle)) {
      groupSortMap.set(tag.groupTitle, tag.groupSortOrder);
    }
  }
  return [...groupSortMap.keys()].sort(
    (a, b) => (groupSortMap.get(a) ?? 0) - (groupSortMap.get(b) ?? 0),
  );
});

const allSelectedTags = computed(
  () =>
    new Set([
      ...filterState.value.requiredTags,
      ...filterState.value.excludedTags,
    ]),
);

const tagGroupOptions = computed(() =>
  tagGroups.value
    .map((groupName) => {
      const groupTags = tags.value.filter(
        (t) => t.groupTitle === groupName && !allSelectedTags.value.has(t.id),
      );
      if (groupTags.length === 0) return null;
      const groupDesc = groupTags[0]?.groupDescription;
      return { value: groupName, label: groupName, hint: groupDesc };
    })
    .filter(
      (o): o is { value: string; label: string; hint: string | undefined } =>
        o !== null,
    ),
);

const tagsInGroup = computed(() => {
  const query = tagSearchQuery.value.toLowerCase().trim();
  const group = navPath.value?.group;

  if (query && !group) {
    return tags.value
      .filter((t) => !allSelectedTags.value.has(t.id))
      .filter((t) => t.title.toLowerCase().includes(query))
      .sort(byTagOrder)
      .map((t) => ({
        value: String(t.id),
        label: t.title,
        hint: descriptionText(t.description),
      }));
  }

  if (group) {
    return tags.value
      .filter((t) => t.groupTitle === group && !allSelectedTags.value.has(t.id))
      .filter((t) => !query || t.title.toLowerCase().includes(query))
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((t) => ({
        value: String(t.id),
        label: t.title,
        hint: descriptionText(t.description),
      }));
  }

  return [];
});

function selectTagGroup(groupName: string) {
  navPath.value = { filter: navPath.value?.filter ?? "tag", group: groupName };
}

function selectTag(tagIdStr: string) {
  const tagId = parseInt(tagIdStr, 10);
  if (navPath.value?.filter === "tag") {
    addRequiredTag(tagId);
  } else if (navPath.value?.filter === "excludeTag") {
    addExcludedTag(tagId);
  }
  // Stays open for the next tag.
}
</script>

<template>
  <div class="dropdown-search">
    <input
      v-model="tagSearchQuery"
      type="text"
      class="dropdown-search-input"
      :placeholder="navPath?.group ? 'Поиск тега в группе' : 'Поиск тега'"
      :aria-label="navPath?.group ? 'Поиск тега в группе' : 'Поиск тега'"
    />
  </div>

  <div v-if="tagsError && !tagsCame" class="dropdown-empty">
    Не удалось загрузить теги
    <button type="button" class="retry" @click="gamesStore.fetchTags(true)">
      Повторить
    </button>
  </div>

  <template v-if="!navPath?.group && !tagSearchQuery">
    <FilterDropdownItem
      v-for="group in tagGroupOptions"
      :key="group.value"
      :label="group.label"
      :hint="group.hint"
      has-sub-options
      @item-select="selectTagGroup(group.value)"
    />
  </template>

  <template v-else>
    <FilterDropdownItem
      v-for="tag in tagsInGroup"
      :key="tag.value"
      :label="tag.label"
      :hint="tag.hint"
      @item-select="selectTag(tag.value)"
    />
    <div v-if="tagsCame && tagsInGroup.length === 0" class="dropdown-empty">
      {{ NOTHING_FOUND }}
    </div>
  </template>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Filters" as *
@use "@/assets/styles/Links" as *

// The menu renders straight into the panel, so the panel's rules are its own too.
+dropdown-search-input
+filter-dropdown-base

.retry
  margin-left: $small
  +inline-link-button
</style>
