<script setup lang="ts">
import { computed, onMounted } from "vue";
import { vClickOutside } from "@/shared/directives";
import {
  useGamesFilter,
  STATUS_OPTIONS,
  RECRUITMENT_FILTER_OPTIONS,
  CLOSED_REASON_FILTER_OPTIONS,
  SORT_OPTIONS,
} from "../model";
import type {
  RecruitmentFilter,
  ClosedReasonFilter,
  StatusValue,
} from "../model";
import { storeToRefs } from "pinia";
import { useGamesStore, type Tag } from "@/entities/game";
import { useFilterSearch, useFilterDropdown } from "@/shared/lib/composables";
import { formatDateForDisplay } from "@/shared/lib/filters";
import {
  FilterSearchInput,
  FilterButton,
  FilterDropdown,
  FilterDropdownHeader,
  FilterDropdownItem,
  DateRangePicker,
  OptionsList,
  SortButton,
  ExpandableBubble,
  FilterBubble,
  BubblesRow,
} from "@/shared/ui/Filters";
import { UserMultiSelect } from "@/entities/user";

const {
  filterState,
  setSearch,
  setStatus,
  clearStatus,
  setRecruitmentFilter,
  setClosedReasonFilter,
  addRequiredTag,
  addExcludedTag,
  removeTag,
  addHost,
  removeHost,
  setCreatedRange,
  setActivatedRange,
  setClosedRange,
  setRecruitmentStartedRange,
  setSort,
  toggleSortOrder,
  clearFilters,
  validateTagFilters,
} = useGamesFilter();

// =============================================================================
// SEARCH INPUT (with debounce)
// =============================================================================

const { localInput, handleInput, applySearch } = useFilterSearch(
  computed(() => filterState.value.search),
  setSearch,
);

// =============================================================================
// FILTER DROPDOWN (with navigation)
// =============================================================================

const {
  showDropdown,
  navPath,
  closeDropdown: closeDropdownBase,
  toggleDropdown,
} = useFilterDropdown();

function closeDropdown() {
  closeDropdownBase();
  tagSearchQuery.value = "";
}

// Root level filter options
const filterOptions = [
  {
    key: "status",
    label: "Статус игры",
    hint: "Оформляется, Идет игра, Закрыта",
  },
  { key: "host", label: "Ведущие", hint: "Мастер или ассистент" },
  { key: "dates", label: "Даты", hint: "Фильтр по датам" },
  { key: "tag", label: "Тег", hint: "Обязательный тег" },
  { key: "excludeTag", label: "Без тега", hint: "Исключить тег" },
];

// Date type options (level 2)
const dateTypeOptions = [
  { value: "created", label: "Создание игры", hint: "По дате создания игры" },
  { value: "activated", label: "Начало игры", hint: "По дате начала игры" },
  {
    value: "recruitmentstarted",
    label: "Начало последнего набора",
    hint: "По дате последнего набора",
  },
  { value: "closed", label: "Закрытие игры", hint: "По дате закрытия игры" },
];

function selectRootItem(key: string) {
  navPath.value = { filter: key };
  if (key === "tag" || key === "excludeTag") {
    tagSearchQuery.value = "";
  }
}

function navigateBack() {
  if (!navPath.value) return;

  // Level 4: recruitment sub-options → level 3: Active sub-options
  if (navPath.value.recruitment) {
    navPath.value = {
      filter: navPath.value.filter,
      status: navPath.value.status,
    };
    return;
  }

  // Level 3: inside group or status sub-options → level 2
  if (navPath.value.group || navPath.value.status) {
    navPath.value = { filter: navPath.value.filter };
    tagSearchQuery.value = "";
    return;
  }

  // Level 2 → root
  navPath.value = null;
  tagSearchQuery.value = "";
}

function getDropdownTitle(): string | null {
  if (!navPath.value) return null;

  if (navPath.value.filter === "status") {
    if (navPath.value.recruitment) return "Набор игроков";
    if (navPath.value.status === "Active") return "Идет игра";
    if (navPath.value.status === "Closed") return "Закрыта";
    return "Статус игры";
  }
  if (navPath.value.filter === "host") return "Ведущие";
  if (navPath.value.filter === "dates") {
    if (navPath.value.group === "created") return "Создание игры";
    if (navPath.value.group === "activated") return "Начало игры";
    if (navPath.value.group === "recruitmentstarted")
      return "Начало последнего набора";
    if (navPath.value.group === "closed") return "Закрытие игры";
    return "Даты";
  }
  if (navPath.value.filter === "tag") return navPath.value.group || "Тег";
  if (navPath.value.filter === "excludeTag")
    return navPath.value.group || "Без тега";

  return null;
}

// =============================================================================
// TAGS
// =============================================================================

import { ref } from "vue";

const gamesStore = useGamesStore();
const { tags: storeTags } = storeToRefs(gamesStore);

// Tags come from the cached store (shared with the table and the tag
// cloud) - one HTTP request per page instead of a duplicate direct call
const tags = computed(() => storeTags.value ?? []);
const tagSearchQuery = ref("");

// Ensure tags are loaded (store dedupes concurrent fetches)
onMounted(async () => {
  await gamesStore.fetchTags();
  if (storeTags.value) {
    validateTagFilters(new Set(storeTags.value.map((t) => t.id)));
  }
});

// Tag groups (sorted by API-provided groupSortOrder)
const tagGroups = computed(() => {
  const groupSortMap = new Map<string, number>();
  for (const tag of tags.value) {
    if (tag.groupTitle && !groupSortMap.has(tag.groupTitle)) {
      groupSortMap.set(tag.groupTitle, tag.groupSortOrder);
    }
  }
  return [...groupSortMap.keys()].sort((a, b) => {
    return (groupSortMap.get(a) ?? 0) - (groupSortMap.get(b) ?? 0);
  });
});

// Tag map for quick lookup
const tagMap = computed(() => {
  const map: Record<number, Tag> = {};
  for (const tag of tags.value) map[tag.id] = tag;
  return map;
});

// All selected tags (required + excluded)
const allSelectedTags = computed(
  () =>
    new Set([
      ...filterState.value.requiredTags,
      ...filterState.value.excludedTags,
    ]),
);

// Strip tooltip markup for display
function stripRichTextMarkup(text: string | undefined): string | undefined {
  if (!text) return text;
  return text.replace(/\[(tipimg|tip):[^\]]+\]([^[]*)\[\/\1\]/g, "$2");
}

// Tags for current level (groups or tags in group)
const tagGroupOptions = computed(() => {
  return tagGroups.value
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
    );
});

// Tags in selected group (or all matching tags when searching)
const tagsInGroup = computed(() => {
  const query = tagSearchQuery.value.toLowerCase().trim();
  const group = navPath.value?.group;

  // If searching, show flat list of matching tags
  if (query && !group) {
    return tags.value
      .filter((t) => !allSelectedTags.value.has(t.id))
      .filter((t) => t.title.toLowerCase().includes(query))
      .sort((a, b) => {
        if (a.groupSortOrder !== b.groupSortOrder)
          return a.groupSortOrder - b.groupSortOrder;
        return a.sortOrder - b.sortOrder;
      })
      .map((t) => ({
        value: String(t.id),
        label: t.title,
        hint: stripRichTextMarkup(t.description),
      }));
  }

  // Tags in selected group
  if (group) {
    return tags.value
      .filter((t) => t.groupTitle === group && !allSelectedTags.value.has(t.id))
      .filter((t) => !query || t.title.toLowerCase().includes(query))
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((t) => ({
        value: String(t.id),
        label: t.title,
        hint: stripRichTextMarkup(t.description),
      }));
  }

  return [];
});

function selectTagGroup(groupName: string) {
  navPath.value = { filter: navPath.value?.filter ?? "tag", group: groupName };
  tagSearchQuery.value = "";
}

function selectTag(tagIdStr: string) {
  const tagId = parseInt(tagIdStr, 10);
  if (navPath.value?.filter === "tag") {
    addRequiredTag(tagId);
  } else if (navPath.value?.filter === "excludeTag") {
    addExcludedTag(tagId);
  }
  // Stay in dropdown to allow selecting more tags
}

// =============================================================================
// STATUS (hierarchical navigation)
// =============================================================================

// Status options (level 2)
const statusOptions = [
  {
    value: "Draft",
    label: "Оформляется",
    hint: "Оформляющиеся игры",
    hasSubOptions: false,
  },
  {
    value: "Active",
    label: "Идет игра",
    hint: "Активные игры",
    hasSubOptions: true,
  },
  {
    value: "Closed",
    label: "Закрыта",
    hint: "Закрытые игры",
    hasSubOptions: true,
  },
];

// Active sub-options (level 3)
const activeSubOptions = [
  { value: "any", label: "Все", hint: "Все активные игры" },
  {
    value: "recruiting",
    label: "Набор игроков",
    hint: "Игры с открытым набором",
    hasSubOptions: true,
  },
  { value: "closed", label: "Набор закрыт", hint: "Игры без набора" },
];

// Recruitment sub-options (level 4)
const recruitmentSubOptions = [
  { value: "open", label: "Все", hint: "Любой тип набора" },
  { value: "initial", label: "Первый набор", hint: "Новые игры, первый набор" },
  {
    value: "subsequent",
    label: "Донабор игроков",
    hint: "Продолжающиеся игры, повторный набор",
  },
];

// Closed sub-options (level 3)
const closedSubOptions = [
  { value: "any", label: "Все", hint: "Все закрытые игры" },
  { value: "None", label: "Без флагов", hint: "Игра закрыта" },
  {
    value: "Frozen",
    label: "Заморожена",
    hint: "Игра временно приостановлена",
  },
  { value: "Finished", label: "Завершена", hint: "Игра доведена до финала" },
];

function handleStatusSelect(value: string) {
  const option = statusOptions.find((o) => o.value === value);
  if (option?.hasSubOptions) {
    navPath.value = { filter: "status", status: value };
  } else {
    // Direct status selection (Draft)
    setStatus(value as StatusValue);
    closeDropdown();
  }
}

function handleActiveSubSelect(value: string) {
  if (value === "recruiting") {
    // Navigate to recruitment sub-options
    navPath.value = { filter: "status", status: "Active", recruitment: true };
  } else if (value === "any") {
    setStatus("Active");
    setRecruitmentFilter("any");
    closeDropdown();
  } else if (value === "closed") {
    setStatus("Active");
    setRecruitmentFilter("closed");
    closeDropdown();
  }
}

function handleRecruitmentSubSelect(value: string) {
  setStatus("Active");
  setRecruitmentFilter(value as RecruitmentFilter);
  closeDropdown();
}

function handleClosedSubSelect(value: string) {
  setStatus("Closed");
  setClosedReasonFilter(value as ClosedReasonFilter);
  closeDropdown();
}

// =============================================================================
// HOSTS
// =============================================================================

function handleAddHost(username: string) {
  addHost(username);
}

// =============================================================================
// DATE RANGES
// =============================================================================

function selectDateType(value: string) {
  navPath.value = { filter: "dates", group: value };
}

function getCurrentDateValues(): { from: string | null; to: string | null } {
  const group = navPath.value?.group;
  if (group === "created") {
    return {
      from: filterState.value.createdFromUtc,
      to: filterState.value.createdToUtc,
    };
  }
  if (group === "activated") {
    return {
      from: filterState.value.activatedFromUtc,
      to: filterState.value.activatedToUtc,
    };
  }
  if (group === "recruitmentstarted") {
    return {
      from: filterState.value.recruitmentStartedFromUtc,
      to: filterState.value.recruitmentStartedToUtc,
    };
  }
  if (group === "closed") {
    return {
      from: filterState.value.closedFromUtc,
      to: filterState.value.closedToUtc,
    };
  }
  return { from: null, to: null };
}

function handleDateApply(from: string | null, to: string | null) {
  const group = navPath.value?.group;
  if (group === "created") {
    setCreatedRange(from, to);
  } else if (group === "activated") {
    setActivatedRange(from, to);
  } else if (group === "recruitmentstarted") {
    setRecruitmentStartedRange(from, to);
  } else if (group === "closed") {
    setClosedRange(from, to);
  }
  closeDropdown();
}

function handleDateClear() {
  const group = navPath.value?.group;
  if (group === "created") {
    setCreatedRange(null, null);
  } else if (group === "activated") {
    setActivatedRange(null, null);
  } else if (group === "recruitmentstarted") {
    setRecruitmentStartedRange(null, null);
  } else if (group === "closed") {
    setClosedRange(null, null);
  }
  closeDropdown();
}

function hasCurrentDateFilter(): boolean {
  const { from, to } = getCurrentDateValues();
  return from !== null || to !== null;
}

// =============================================================================
// SORT
// =============================================================================

const sortOptions = SORT_OPTIONS.map((o) => ({
  value: o.value,
  label: o.label,
  hint: o.hint,
  defaultDirection: o.defaultDirection,
}));

function handleSortByChange(value: string) {
  const option = SORT_OPTIONS.find((o) => o.value === value);
  setSort(value, option?.defaultDirection);
}

function handleSortOrderChange(order: "asc" | "desc") {
  if (order !== filterState.value.sortOrder) {
    toggleSortOrder();
  }
}

// =============================================================================
// BUBBLES
// =============================================================================

const hasStatusFilter = computed(() => filterState.value.status !== null);
const statusBubbleLabel = computed(() => {
  const status = filterState.value.status;
  if (!status) return "";

  let label = STATUS_OPTIONS.find((o) => o.value === status)?.label ?? status;
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

// Date range filters
const hasCreatedDateFilter = computed(
  () =>
    filterState.value.createdFromUtc !== null ||
    filterState.value.createdToUtc !== null,
);
const createdDateLabel = computed(() => {
  const from = formatDateForDisplay(filterState.value.createdFromUtc);
  const to = formatDateForDisplay(filterState.value.createdToUtc);
  if (from && to) return `${from} — ${to}`;
  if (from) return `с ${from}`;
  if (to) return `до ${to}`;
  return "";
});

const hasActivatedDateFilter = computed(
  () =>
    filterState.value.activatedFromUtc !== null ||
    filterState.value.activatedToUtc !== null,
);
const activatedDateLabel = computed(() => {
  const from = formatDateForDisplay(filterState.value.activatedFromUtc);
  const to = formatDateForDisplay(filterState.value.activatedToUtc);
  if (from && to) return `${from} — ${to}`;
  if (from) return `с ${from}`;
  if (to) return `до ${to}`;
  return "";
});

const hasRecruitmentStartedDateFilter = computed(
  () =>
    filterState.value.recruitmentStartedFromUtc !== null ||
    filterState.value.recruitmentStartedToUtc !== null,
);
const recruitmentStartedDateLabel = computed(() => {
  const from = formatDateForDisplay(
    filterState.value.recruitmentStartedFromUtc,
  );
  const to = formatDateForDisplay(filterState.value.recruitmentStartedToUtc);
  if (from && to) return `${from} — ${to}`;
  if (from) return `с ${from}`;
  if (to) return `до ${to}`;
  return "";
});

const hasClosedDateFilter = computed(
  () =>
    filterState.value.closedFromUtc !== null ||
    filterState.value.closedToUtc !== null,
);
const closedDateLabel = computed(() => {
  const from = formatDateForDisplay(filterState.value.closedFromUtc);
  const to = formatDateForDisplay(filterState.value.closedToUtc);
  if (from && to) return `${from} — ${to}`;
  if (from) return `с ${from}`;
  if (to) return `до ${to}`;
  return "";
});

// Tags bubbles
const requiredTagsBubbles = computed(() => {
  return [...filterState.value.requiredTags]
    .map((tagId) => ({
      tagId,
      title: tagMap.value[tagId]?.title ?? `#${tagId}`,
      groupSortOrder: tagMap.value[tagId]?.groupSortOrder ?? 99,
      sortOrder: tagMap.value[tagId]?.sortOrder ?? 99,
    }))
    .sort((a, b) => {
      if (a.groupSortOrder !== b.groupSortOrder)
        return a.groupSortOrder - b.groupSortOrder;
      return a.sortOrder - b.sortOrder;
    });
});

const excludedTagsBubbles = computed(() => {
  return [...filterState.value.excludedTags]
    .map((tagId) => ({
      tagId,
      title: tagMap.value[tagId]?.title ?? `#${tagId}`,
      groupSortOrder: tagMap.value[tagId]?.groupSortOrder ?? 99,
      sortOrder: tagMap.value[tagId]?.sortOrder ?? 99,
    }))
    .sort((a, b) => {
      if (a.groupSortOrder !== b.groupSortOrder)
        return a.groupSortOrder - b.groupSortOrder;
      return a.sortOrder - b.sortOrder;
    });
});

// Hosts bubble values
const hostsBubbleValues = computed(() =>
  [...filterState.value.hostUsernames]
    .sort((a, b) => a.toLowerCase().localeCompare(b.toLowerCase(), "ru"))
    .map((username) => ({ id: username, label: username })),
);

function handleRemoveHost(id: string) {
  removeHost(id);
}

// Only when at least one actual filter bubble is rendered; non-default
// sorting alone must not show a row with a lone "Сбросить"
const hasBubbles = computed(() => {
  const state = filterState.value;
  return (
    hasStatusFilter.value ||
    state.hostUsernames.size > 0 ||
    hasCreatedDateFilter.value ||
    hasActivatedDateFilter.value ||
    hasRecruitmentStartedDateFilter.value ||
    hasClosedDateFilter.value ||
    state.requiredTags.size > 0 ||
    state.excludedTags.size > 0
  );
});

function clearAll() {
  localInput.value = "";
  clearFilters();
}

// Search input keyboard handler (for Backspace to remove filters)
function handleSearchKeydown(event: KeyboardEvent) {
  if (event.key === "Backspace" && !localInput.value) {
    // Remove filters in reverse order
    if (filterState.value.excludedTags.size > 0) {
      event.preventDefault();
      const lastTag = [...filterState.value.excludedTags].pop();
      if (lastTag) removeTag(lastTag);
      return;
    }
    if (filterState.value.requiredTags.size > 0) {
      event.preventDefault();
      const lastTag = [...filterState.value.requiredTags].pop();
      if (lastTag) removeTag(lastTag);
      return;
    }
    if (hasClosedDateFilter.value) {
      event.preventDefault();
      setClosedRange(null, null);
      return;
    }
    if (hasRecruitmentStartedDateFilter.value) {
      event.preventDefault();
      setRecruitmentStartedRange(null, null);
      return;
    }
    if (hasActivatedDateFilter.value) {
      event.preventDefault();
      setActivatedRange(null, null);
      return;
    }
    if (hasCreatedDateFilter.value) {
      event.preventDefault();
      setCreatedRange(null, null);
      return;
    }
    if (filterState.value.hostUsernames.size > 0) {
      event.preventDefault();
      const lastHost = [...filterState.value.hostUsernames].pop();
      if (lastHost) removeHost(lastHost);
      return;
    }
    if (hasStatusFilter.value) {
      event.preventDefault();
      clearStatus();
      return;
    }
  }
}
</script>

<template>
  <div class="games-filter">
    <!-- Filter bar row -->
    <div class="filter-bar">
      <!-- Search input -->
      <FilterSearchInput
        v-model="localInput"
        placeholder="Поиск по названию"
        @input="handleInput"
        @keydown="handleSearchKeydown"
        @blur="applySearch"
      />

      <!-- Filter button -->
      <div v-click-outside="closeDropdown" class="filter-section">
        <FilterButton
          :active="showDropdown"
          @click="toggleDropdown"
          @close="closeDropdown"
        />

        <FilterDropdown v-if="showDropdown" @close="closeDropdown">
          <!-- Navigation header when inside a sub-level -->
          <FilterDropdownHeader
            v-if="navPath"
            :title="getDropdownTitle() ?? ''"
            @back="navigateBack"
          />

          <!-- Root level items -->
          <template v-if="!navPath">
            <FilterDropdownItem
              v-for="item in filterOptions"
              :key="item.key"
              :label="item.label"
              :hint="item.hint"
              has-sub-options
              @item-select="selectRootItem(item.key)"
            />
          </template>

          <!-- STATUS NAVIGATION -->
          <!-- Level 2: Status options -->
          <OptionsList
            v-if="navPath?.filter === 'status' && !navPath.status"
            :options="statusOptions"
            @select="handleStatusSelect"
          />

          <!-- Level 3: Active sub-options -->
          <OptionsList
            v-if="
              navPath?.filter === 'status' &&
              navPath.status === 'Active' &&
              !navPath.recruitment
            "
            :options="activeSubOptions"
            @select="handleActiveSubSelect"
          />

          <!-- Level 4: Recruitment sub-options -->
          <OptionsList
            v-if="
              navPath?.filter === 'status' &&
              navPath.status === 'Active' &&
              navPath.recruitment
            "
            :options="recruitmentSubOptions"
            @select="handleRecruitmentSubSelect"
          />

          <!-- Level 3: Closed sub-options -->
          <OptionsList
            v-if="navPath?.filter === 'status' && navPath.status === 'Closed'"
            :options="closedSubOptions"
            @select="handleClosedSubSelect"
          />

          <!-- HOSTS: include-inactive so masters of archived games
               (inactive 30+ days) stay findable -->
          <UserMultiSelect
            v-if="navPath?.filter === 'host'"
            :selected-users="filterState.hostUsernames"
            placeholder="Поиск ведущего"
            include-inactive
            @add="handleAddHost"
            @remove="handleRemoveHost"
          />

          <!-- DATES NAVIGATION -->
          <!-- Level 2: Date type selection -->
          <template v-if="navPath?.filter === 'dates' && !navPath.group">
            <FilterDropdownItem
              v-for="item in dateTypeOptions"
              :key="item.value"
              :label="item.label"
              :hint="item.hint"
              has-sub-options
              @item-select="selectDateType(item.value)"
            />
          </template>

          <!-- Level 3: Date range picker -->
          <DateRangePicker
            v-if="navPath?.filter === 'dates' && navPath.group"
            :from-value="getCurrentDateValues().from"
            :to-value="getCurrentDateValues().to"
            :show-clear-button="hasCurrentDateFilter()"
            @apply="handleDateApply"
            @clear="handleDateClear"
          />

          <!-- TAGS NAVIGATION -->
          <!-- Tag search input -->
          <div
            v-if="navPath?.filter === 'tag' || navPath?.filter === 'excludeTag'"
            class="dropdown-search"
          >
            <input
              v-model="tagSearchQuery"
              type="text"
              class="dropdown-search-input"
              :placeholder="
                navPath?.group ? 'Поиск тега в группе' : 'Поиск тега'
              "
            />
          </div>

          <!-- Level 2: Tag groups (when not searching) -->
          <template
            v-if="
              (navPath?.filter === 'tag' || navPath?.filter === 'excludeTag') &&
              !navPath.group &&
              !tagSearchQuery
            "
          >
            <FilterDropdownItem
              v-for="group in tagGroupOptions"
              :key="group.value"
              :label="group.label"
              :hint="group.hint"
              has-sub-options
              @item-select="selectTagGroup(group.value)"
            />
          </template>

          <!-- Level 3: Tags in group OR search results -->
          <template
            v-if="
              (navPath?.filter === 'tag' || navPath?.filter === 'excludeTag') &&
              (navPath.group || tagSearchQuery)
            "
          >
            <FilterDropdownItem
              v-for="tag in tagsInGroup"
              :key="tag.value"
              :label="tag.label"
              :hint="tag.hint"
              @item-select="selectTag(tag.value)"
            />
            <div v-if="tagsInGroup.length === 0" class="dropdown-empty">
              Ничего не найдено
            </div>
          </template>
        </FilterDropdown>
      </div>

      <!-- Sort -->
      <SortButton
        :options="sortOptions"
        :sort-by="filterState.sortBy"
        :sort-order="filterState.sortOrder"
        @update:sort-by="handleSortByChange"
        @update:sort-order="handleSortOrderChange"
      />
    </div>

    <!-- Active filters row (bubbles) -->
    <BubblesRow v-if="hasBubbles" @clear-all="clearAll">
      <!-- Status bubble -->
      <FilterBubble
        v-if="hasStatusFilter"
        prefix="Статус игры:"
        :value="statusBubbleLabel"
        @remove="clearStatus"
      />

      <!-- Hosts bubble -->
      <ExpandableBubble
        v-if="filterState.hostUsernames.size > 0"
        :prefix="filterState.hostUsernames.size > 1 ? 'Ведущие:' : 'Ведущий:'"
        :values="hostsBubbleValues"
        :max-visible="1"
        @remove="handleRemoveHost"
      />

      <!-- Created date bubble -->
      <FilterBubble
        v-if="hasCreatedDateFilter"
        prefix="Создание:"
        :value="createdDateLabel"
        @remove="setCreatedRange(null, null)"
      />

      <!-- Activated date bubble -->
      <FilterBubble
        v-if="hasActivatedDateFilter"
        prefix="Начало игры:"
        :value="activatedDateLabel"
        @remove="setActivatedRange(null, null)"
      />

      <!-- Recruitment started date bubble -->
      <FilterBubble
        v-if="hasRecruitmentStartedDateFilter"
        prefix="Начало набора:"
        :value="recruitmentStartedDateLabel"
        @remove="setRecruitmentStartedRange(null, null)"
      />

      <!-- Closed date bubble -->
      <FilterBubble
        v-if="hasClosedDateFilter"
        prefix="Закрытие:"
        :value="closedDateLabel"
        @remove="setClosedRange(null, null)"
      />

      <!-- Required tags bubbles -->
      <FilterBubble
        v-for="tag in requiredTagsBubbles"
        :key="`required-${tag.tagId}`"
        prefix="Тег:"
        :value="tag.title"
        @remove="removeTag(tag.tagId)"
      />

      <!-- Excluded tags bubbles -->
      <FilterBubble
        v-for="tag in excludedTagsBubbles"
        :key="`excluded-${tag.tagId}`"
        prefix="Без тега:"
        :value="tag.title"
        @remove="removeTag(tag.tagId)"
      />
    </BubblesRow>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Filters"

.games-filter
  display: flex
  flex-direction: column
  gap: $small

  +filter-bar
  +filter-button
  +filter-responsive

// Search input inside dropdown
+dropdown-search-input
+filter-dropdown-base
</style>
