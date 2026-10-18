<script setup lang="ts">
/**
 * ProfileGamesTable — the user's games as a forum-style table (search +
 * sort + paging) with a "Ведущий" / "Игрок" role toggle. Profile-scoped:
 * the author is pinned to this profile's username, so it cannot reuse the
 * URL-bound GamesDataTable widget (that one writes the global /games filter
 * state to the route query). Instead it drives the same `gameApi.searchGames`
 * endpoint with `hostUsernames=[username]` (host) or `playerUsername` +
 * `playerParticipation=Any` (player), keeping the table UI/columns visually
 * aligned with /games without duplicating its filter machinery. The Any
 * scope is profile-specific: the profile is a dossier, so it also lists
 * games where the user only has retired characters or a pending
 * application — unlike the public /games player filter (active-only).
 *
 * Four states: loading skeleton (DataTable) → error line → empty
 * (two-state) → content.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CounterPair } from "@/shared/ui/CounterPair";
import { FilterSearchInput, SortButton } from "@/shared/ui/Filters";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";
import Paging from "@/shared/ui/Paging/Paging.vue";
import {
  gameApi,
  GameStatusBadge,
  GameStatus,
  useGameDisplay,
  type Game,
  type PlayerCharacterInfo,
} from "@/entities/game";
import { RemovedAccountLink, UserLink } from "@/entities/user";
import type { ListEnvelope } from "@/shared/api/models/common";
import { buildReadersTooltip } from "@/shared/lib/utils/tooltipBuilders";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

const props = defineProps<{
  /** Profile owner whose games we list. */
  username: string;
}>();

type RoleScope = "host" | "player";

const route = useRoute();
const router = useRouter();

const {
  buildStatusTooltip,
  getUnreadPosts,
  getUnreadComments,
  formatUnreadPostsTooltip,
  formatUnreadCommentsTooltip,
  isNew,
  formatSlots,
  buildSlotsTooltip,
  buildAssistantTooltip,
} = useGameDisplay();

// Title-link colour convention (same as GameLink): closed games are muted
// grey; the green "new" highlight excludes closed (muted takes priority).
function isClosedGame(game: Game): boolean {
  return game.status === GameStatus.Closed;
}
function isNewHighlight(game: Game): boolean {
  return !isClosedGame(game) && isNew(game);
}

const role = ref<RoleScope>("host");
// Tracks whether the user has manually touched the role toggle — gates the
// one-time auto-switch below so it never fights a deliberate click.
const roleTouchedManually = ref(false);
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
    : {
        ...base,
        playerUsername: props.username,
        // Dossier scope: include games with only retired characters or a
        // pending application, not just active participation.
        playerParticipation: "Any" as const,
      };
});

const envelope = ref<ListEnvelope<Game> | null>(null);

// clearErrorOnStart: what this table did before the composable — the error line
// goes away while the next page loads.
const { loading, error, run } = useGuardedRequest({
  message: "Не удалось загрузить игры",
  clearErrorOnStart: true,
});

function fetchGames() {
  const roleAtRequest = role.value;
  return run(
    () => gameApi.searchGames(apiParams.value),
    (data) => {
      envelope.value = data;

      // Self-contained auto-switch: if the *initial default* "Ведущий"
      // fetch comes back empty and the user hasn't touched the toggle yet, flip
      // once to "Игрок" so a profile with no hosted games doesn't land on a
      // dead tab. Restricted to the unfiltered first load (no search typed
      // yet) — an empty search result for "host" must not trigger this, only
      // a genuinely empty default fetch. Any later manual toggle, or an empty
      // "player" result, must NOT bounce back.
      if (
        roleAtRequest === "host" &&
        !roleTouchedManually.value &&
        !search.value &&
        (data?.paging?.total ?? 0) === 0
      ) {
        role.value = "player";
      }
    },
  );
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
  // Any explicit click — even re-clicking the already-active role — counts
  // as a manual choice and permanently disarms the auto-switch.
  roleTouchedManually.value = true;
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

// aria-sort only announces columns the table actually renders: "created"
// has no column here (same as /games), so sort state maps to "title" only.
const currentSort = computed<SortState | undefined>(() =>
  sortBy.value === "title"
    ? { key: "title", direction: sortOrder.value }
    : undefined,
);

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

function handleSortSelect(value: string, direction?: "asc" | "desc") {
  sortBy.value = value === "title" ? "title" : "created";
  sortOrder.value = direction ?? "desc";
}

function handleSortOrder(order: "asc" | "desc") {
  sortOrder.value = order;
}

// Column sets mirror /games (GamesDataTable): same keys, order, widths and
// cell formats, minus the tags column (profile-specific decision). In
// "Игрок" mode the tags' slot is taken by the profile-only "Статус
// персонажа" column (the backend fills `playerCharacters` solely when the
// request carries the playerUsername filter, so in "Ведущий" mode there is
// nothing to show there); in "Ведущий" mode that slot is folded back into
// title/master/status. "Название" takes what the others leave.
const columns = computed<Column[]>(() =>
  role.value === "player"
    ? [
        { key: "title", label: "Название", align: "left" },
        { key: "master", label: "Ведущие", width: "17.4%", align: "left" },
        { key: "status", label: "Статус игры", width: "15.9%", align: "left" },
        {
          key: "character",
          label: "Статус персонажа",
          width: "19.2%",
          align: "left",
        },
        {
          key: "reviews",
          label: "Рецензии",
          width: "8.5%",
          align: "center",
        },
        { key: "readers", label: "Читатели", width: "7.9%", align: "center" },
      ]
    : [
        { key: "title", label: "Название", align: "left" },
        { key: "master", label: "Ведущие", width: "22.2%", align: "left" },
        { key: "status", label: "Статус игры", width: "20.7%", align: "left" },
        {
          key: "reviews",
          label: "Рецензии",
          width: "8.6%",
          align: "center",
        },
        { key: "readers", label: "Читатели", width: "8%", align: "center" },
      ],
);

// Character status wording — the captions of the CharacterCard badge (CharacterStatus
// refined by the "out of game" flags). Standalone cell value, so it is
// capitalized; no color coding.
function characterStatusLabel(ch: PlayerCharacterInfo): string {
  switch (ch.status) {
    case "Active":
      return "В игре";
    case "Retired":
      if (ch.isDead) return "Персонаж мертв";
      if (ch.isPlayerExiled) return "Выведен из игры";
      return "Покинул игру";
    case "UnderReview":
      return "Заявка на рассмотрении";
    default:
      return "";
  }
}

// Paging scrolls the games table back into view (not the page top)
const tableRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return tableRef.value?.$el ?? null;
}
</script>

<template>
  <div class="profile-games-table">
    <!-- The field takes the line; where the sort and the toggle do not fit
         beside it, they go under it together, side by side or one under the
         other, the sort with its words, the toggle with both segments. -->
    <div class="controls">
      <FilterSearchInput
        v-model="searchInput"
        placeholder="Поиск по названию"
        class="search"
      />

      <div class="controls-pair">
        <SortButton
          :options="sortOptions"
          :sort-by="sortBy"
          :sort-order="sortOrder"
          @sort-select="handleSortSelect"
          @update:sort-order="handleSortOrder"
        />

        <SegmentedControl
          class="role-toggle"
          :model-value="role"
          :options="[
            { value: 'host', label: 'Ведущий' },
            { value: 'player', label: 'Игрок' },
          ]"
          ariaLabel="Роль в играх"
          @update:model-value="setRole"
        />
      </div>
    </div>

    <ErrorState v-if="error" :message="error" :retry="fetchGames" />

    <DataTable
      v-if="!error || games.length > 0"
      ref="tableRef"
      :columns="columns"
      :data="games"
      :loading="loading"
      :show-row-numbers="true"
      :start-row-number="paging ? paging.skip + 1 : 1"
      :sort="currentSort"
      :empty-text="emptyText"
    >
      <!-- Title column: Title (unread/comments), same format as /games -->
      <template #cell-title="{ row }">
        <router-link
          :to="{ name: 'game', params: { id: row.publicId || row.id } }"
          :class="[
            'game-link',
            {
              'new-item': isNewHighlight(row),
              'closed-item': isClosedGame(row),
            },
          ]"
        >
          <span v-if="search" v-html="highlightMatch(row.title, search)"></span>
          <template v-else>{{ row.title }}</template> </router-link
        >{{ " "
        }}<!-- First-unread endpoints bind the id as a Guid - pass row.id -->
        <CounterPair
          :first-value="getUnreadPosts(row)"
          :first-to="{ name: 'game-first-unread-post', params: { id: row.id } }"
          :first-label="formatUnreadPostsTooltip(getUnreadPosts(row))"
          :second-value="getUnreadComments(row)"
          :second-to="{
            name: 'game-first-unread-comment',
            params: { id: row.id },
          }"
          :second-label="formatUnreadCommentsTooltip(getUnreadComments(row))"
        />
      </template>

      <template #cell-master="{ row }">
        <UserLink
          v-if="row.master"
          :user="row.master"
          hide-badge
        /><RemovedAccountLink v-else /><template v-if="row.assistants?.length"
          >{{ " "
          }}<Tooltip :text="buildAssistantTooltip(row.assistants)">
            <span class="assistant-count">[+{{ row.assistants.length }}]</span>
          </Tooltip></template
        >
      </template>

      <!-- Character status column ("Игрок" mode): just the status word (no
           color, no tooltip). Extra characters show as a plain "[+N]" count;
           the details live on the game page. Inline flow copies as one line. -->
      <template #cell-character="{ row }">
        <template v-if="row.playerCharacters?.length">
          <span class="character-status">{{
            characterStatusLabel(row.playerCharacters[0])
          }}</span
          ><span v-if="row.playerCharacters.length > 1" class="character-extra"
            >{{ " " }}[+{{ row.playerCharacters.length - 1 }}]</span
          >
        </template>
        <span v-else class="character-none">{{ VALUE_UNAVAILABLE }}</span>
      </template>

      <template #cell-status="{ row }">
        <Tooltip :text="buildStatusTooltip(row)" focusable>
          <span class="status-wrapper">
            <GameStatusBadge
              :status="row.status"
              :is-recruiting="row.recruitment?.isOpen"
              :is-subsequent="row.recruitment?.isSubsequent"
              :closed-reason="row.closedReason"
            />
          </span> </Tooltip
        >{{ " "
        }}<Tooltip :text="buildSlotsTooltip(row)" focusable>
          <span class="slots-indicator">{{ formatSlots(row) }}</span>
        </Tooltip>
      </template>

      <!-- Reviews column, same format as /games -->
      <template #cell-reviews="{ row }">
        <router-link
          :to="{
            name: 'game-reviews',
            params: { id: row.publicId || row.id },
          }"
          class="review-link"
          :aria-label="`Рецензий: ${row.gameReviewsCount ?? 0}`"
          >{{ row.gameReviewsCount ?? 0 }}</router-link
        >
      </template>

      <template #cell-readers="{ row }">
        <Tooltip :text="buildReadersTooltip(row)" focusable>
          <span class="readers-count">{{ row.subscribersCount ?? 0 }}</span>
        </Tooltip>
      </template>

      <template v-if="paging && paging.pages > 1" #footer>
        <Paging
          :paging="paging"
          :to="{ name: 'profile', params: { username } }"
          :use-query="true"
          query-key="number"
          :scroll-anchor="pagingAnchor"
        />
      </template>
    </DataTable>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.profile-games-table
  display: flex
  flex-direction: column
  gap: $medium

// As before 5 October 2026, without the sideways scroll the toggle had on a
// phone (owner, 12 October 2026): what does not fit beside the field goes to
// the next line whole, and nothing gives up its words.
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

// The sort and the toggle go to the next line as one: never the sort beside
// the field and the toggle alone under it. There they stand side by side, or
// one under the other where the two do not fit.
.controls-pair
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small

// The toggle stands at the width of its segments and does not give it up.
.role-toggle
  flex: none

.game-link
  color: $link
  &:hover
    color: $link-hover
  &.new-item
    color: $accent-green
    &:hover
      color: $accent-green-hover
  &.closed-item
    color: $text-muted
    &:hover
      color: $link-hover
  // .search-highlight styled globally in Reset.sass

.status-wrapper
  cursor: help

.assistant-count
  color: $text-muted
  cursor: help

// "[+N]" suffix for extra characters — a plain muted count (no tooltip;
// the character details live on the game page).
.character-extra
  color: $text-muted

.character-none
  color: $text-muted

// One token, "[0/∞]": the status wraps between its words, never inside the
// brackets.
.slots-indicator
  color: $text-muted
  cursor: help
  white-space: nowrap

.review-link
  color: $link
  &:hover
    color: $link-hover

.readers-count
  cursor: help
</style>
