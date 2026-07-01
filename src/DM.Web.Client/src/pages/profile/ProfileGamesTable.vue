<script setup lang="ts">
/**
 * ProfileGamesTable — the user's games as a forum-style table (search +
 * sort + paging) with a «Ведущий» / «Игрок» role toggle. Profile-scoped:
 * the author is pinned to this profile's username, so it cannot reuse the
 * URL-bound GamesDataTable widget (that one writes the global /games filter
 * state to the route query). Instead it drives the same `gameApi.searchGames`
 * endpoint with `hostUsernames=[username]` (host) or `playerUsername`
 * (player), keeping the table UI/columns visually aligned with /games
 * without duplicating its filter machinery.
 *
 * Four states: loading skeleton (DataTable) → error line → empty
 * (two-state) → content.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { FilterSearchInput, SortButton } from "@/shared/ui/Filters";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { gameApi, GameStatusBadge, type Game } from "@/entities/game";
import type { ListEnvelope } from "@/shared/api/models/common";
import { buildReadersTooltip } from "@/shared/lib/utils/tooltipBuilders";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";

const props = defineProps<{
  /** Profile owner whose games we list. */
  username: string;
}>();

type RoleScope = "host" | "player";

const route = useRoute();
const router = useRouter();

const role = ref<RoleScope>("host");
// `searchInput` is the immediate v-model; `search` is the debounced value
// that actually drives the request (avoids a fetch per keystroke).
const searchInput = ref("");
const search = ref("");
const sortBy = ref<"created" | "title">("created");
const sortOrder = ref<"asc" | "desc">("desc");

let searchTimer: ReturnType<typeof setTimeout> | undefined;
watch(searchInput, (value) => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    search.value = value.trim();
  }, 300);
});

const PAGE_SIZE = 20;

const pageNumber = computed(() => {
  const raw = route.query.number;
  if (!raw) return 1;
  const n = parseInt(String(raw), 10);
  return !isNaN(n) && n > 0 ? n : 1;
});

const apiParams = computed(() => {
  const base = {
    search: search.value || undefined,
    sortBy: sortBy.value,
    sortOrder: sortOrder.value,
    number: pageNumber.value,
    size: PAGE_SIZE,
  };
  return role.value === "host"
    ? { ...base, hostUsernames: [props.username] }
    : { ...base, playerUsername: props.username };
});

const envelope = ref<ListEnvelope<Game> | null>(null);
const loading = ref(false);
const error = ref(false);

async function fetchGames() {
  loading.value = true;
  error.value = false;
  const { data, error: apiError } = await gameApi.searchGames(apiParams.value);
  loading.value = false;
  if (apiError) {
    error.value = true;
    return;
  }
  envelope.value = data ?? null;
}

const games = computed(() => envelope.value?.resources ?? []);
const paging = computed(() => envelope.value?.paging ?? null);

const hasActiveFilters = computed(() => search.value.trim().length > 0);

const emptyText = computed(() => {
  if (hasActiveFilters.value) return "Игр по заданным фильтрам не найдено";
  return role.value === "host"
    ? "Пользователь не ведет игр"
    : "Пользователь не играет ни в одной игре";
});

// Reset pagination when the search term changes — page 3 of an old query
// is meaningless for a new one and would render a fake-empty table.
watch(search, () => {
  if (route.query.number) {
    const query = { ...route.query };
    delete query.number;
    router.replace({ query });
  }
});

// Re-fetch whenever the effective query changes. Stringify dedupes
// adjacent identical states.
const paramsKey = computed(() => JSON.stringify(apiParams.value));
watch(paramsKey, () => fetchGames(), { immediate: true });

function setRole(next: RoleScope) {
  if (role.value === next) return;
  role.value = next;
  // Reset pagination — the other role likely has a different page count,
  // so keeping `number` could land on an out-of-range (fake-empty) page.
  if (route.query.number) {
    const query = { ...route.query };
    delete query.number;
    router.replace({ query });
  }
}

const currentSort = computed<SortState>(() => ({
  key: sortBy.value,
  direction: sortOrder.value,
}));

// Sorting is driven by the SortButton (same control as the /games page),
// not by clicking column headers.
const sortOptions = [
  {
    value: "created",
    label: "Дата создания",
    defaultDirection: "desc" as const,
  },
  { value: "title", label: "Название", defaultDirection: "asc" as const },
];

function handleSortBy(value: string) {
  sortBy.value = value === "title" ? "title" : "created";
  const option = sortOptions.find((o) => o.value === value);
  sortOrder.value = option?.defaultDirection ?? "desc";
}

function handleSortOrder(order: "asc" | "desc") {
  sortOrder.value = order;
}

const columns: Column[] = [
  {
    key: "title",
    label: "Название",
    width: "34%",
    align: "left",
    sortable: true,
  },
  { key: "master", label: "Ведущие", width: "22%", align: "left" },
  { key: "status", label: "Статус игры", width: "20%", align: "left" },
  { key: "readers", label: "Читатели", width: "10%", align: "center" },
  {
    key: "created",
    label: "Дата создания",
    width: "14%",
    align: "left",
    sortable: true,
    hideOnMobile: true,
  },
];

function buildAssistantTooltip(assistants: { username: string }[]): string {
  const names = assistants.map((a) => a.username).join(", ");
  return `Ассистент${assistants.length > 1 ? "ы" : ""}: ${names}`;
}

function formatSlots(row: Game): string {
  const pcCount = row.recruitment?.pcCount ?? 0;
  const pcLimit = row.recruitment?.pcLimit;
  return pcLimit != null ? `[${pcCount}/${pcLimit}]` : `[${pcCount}/∞]`;
}
</script>

<template>
  <div class="profile-games-table">
    <div class="controls">
      <FilterSearchInput
        v-model="searchInput"
        placeholder="Поиск по названию"
        class="search"
      />

      <SortButton
        :options="sortOptions"
        :sort-by="sortBy"
        :sort-order="sortOrder"
        @update:sort-by="handleSortBy"
        @update:sort-order="handleSortOrder"
      />

      <div class="role-toggle" role="group" aria-label="Роль в играх">
        <button
          type="button"
          class="role-button"
          :class="{ active: role === 'host' }"
          :aria-pressed="role === 'host'"
          @click="setRole('host')"
        >
          Ведущий
        </button>
        <button
          type="button"
          class="role-button"
          :class="{ active: role === 'player' }"
          :aria-pressed="role === 'player'"
          @click="setRole('player')"
        >
          Игрок
        </button>
      </div>
    </div>

    <div v-if="error" class="error-message">Не удалось загрузить игры.</div>

    <DataTable
      v-if="!error || games.length > 0"
      :columns="columns"
      :data="games"
      :loading="loading"
      :sort="currentSort"
      :empty-text="emptyText"
    >
      <template #cell-title="{ row }">
        <router-link
          :to="{ name: 'game', params: { id: row.publicId || row.id } }"
          class="game-link"
        >
          <span v-if="search" v-html="highlightMatch(row.title, search)"></span>
          <template v-else>{{ row.title }}</template>
        </router-link>
      </template>

      <template #cell-master="{ row }">
        <router-link
          v-if="row.master"
          :to="{ name: 'profile', params: { username: row.master.username } }"
          class="master-link"
          >{{ row.master.username }}</router-link
        ><Tooltip
          v-if="row.assistants?.length"
          :text="buildAssistantTooltip(row.assistants)"
        >
          <span class="assistant-count">[+{{ row.assistants.length }}]</span>
        </Tooltip>
      </template>

      <template #cell-status="{ row }">
        <GameStatusBadge
          :status="row.status"
          :is-recruiting="row.recruitment?.isOpen"
          :is-subsequent="row.recruitment?.isSubsequent"
          :closed-reason="row.closedReason"
        /><span class="slots-indicator">{{ formatSlots(row) }}</span>
      </template>

      <template #cell-readers="{ row }">
        <Tooltip :text="buildReadersTooltip(row)">
          <span class="readers-count">{{ row.subscribersCount ?? 0 }}</span>
        </Tooltip>
      </template>

      <template #cell-created="{ row }">
        <HumanDate :date="row.createdUtc" format="DD.MM.YYYY" />
      </template>

      <template v-if="paging && paging.pages > 1" #footer>
        <Paging
          :paging="paging"
          :to="{ name: 'profile', params: { username, tab: 'games' } }"
          :use-query="true"
          query-key="number"
        />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.profile-games-table
  display: flex
  flex-direction: column
  gap: $medium

.controls
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small
  margin-bottom: 0

// Search fills all free space on the left (same as the /games filter bar,
// where the search input is flex: 1); sort + role toggle sit to its right.
.search
  flex: 1
  min-width: 200px

// Segmented role toggle, aligned to the control height of the search / sort
// button so the whole bar reads as one row of controls.
.role-toggle
  display: inline-flex
  height: $control-height
  box-sizing: border-box
  border: 1px solid $border
  border-radius: $button-border-radius
  overflow: hidden

.role-button
  padding: 0 $medium
  background-color: $bg-element
  border: none
  font: inherit
  font-size: $secondary-font-size
  color: $text-muted
  cursor: pointer
  transition: color $transition-fast, background-color $transition-fast

  &:hover
    color: $text

  &.active
    background-color: $bg-element-accent
    color: $text
    font-weight: 700

  & + &
    border-left: 1px solid $border

.error-message
  padding: $medium
  color: $text-on-red
  background-color: $bg-highlight-red
  border-radius: $border-radius

.game-link
  color: $link
  &:hover
    color: $link-hover

.master-link
  color: $link
  &:hover
    color: $link-hover

.assistant-count
  color: $text-muted
  margin-left: 0.25em
  cursor: help

.slots-indicator
  color: $text-muted
  margin-left: 0.35em

.readers-count
  cursor: help
</style>
