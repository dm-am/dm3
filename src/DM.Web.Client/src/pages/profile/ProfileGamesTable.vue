<script setup lang="ts">
/**
 * The user's games as a table with a "Ведущий" / "Игрок" toggle, on the
 * endpoint of /games with the author pinned: GamesDataTable writes the global
 * filter into the address. The player scope is Any, as befits a dossier: games
 * with only retired characters or a pending application are listed too.
 */
import { computed, onBeforeUnmount, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { DataTable, type Column, type SortState } from "@/shared/ui/DataTable";
import { Tooltip } from "@/shared/ui/Tooltip";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CounterPair } from "@/shared/ui/CounterPair";
import { FilterSearchInput, SortButton } from "@/shared/ui/Filters";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";
import Paging from "@/shared/ui/Paging/Paging.vue";
import {
  characterStatusLabel,
  gameCatalogApi,
  GameStatusBadge,
  GameStatus,
  useGameDisplay,
  type Character,
  type Game,
  type PlayerCharacterInfo,
} from "@/entities/game";
import { RemovedAccountLink, UserLink } from "@/entities/user";
import {
  GAME_COLUMNS,
  GAME_COLUMN_WIDTHS_WITHOUT_TAGS,
} from "@/widgets/games-table";
import type { ListEnvelope } from "@/shared/api/models/common";
import { buildReadersTooltip } from "@/shared/lib/utils/tooltipBuilders";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { parsePageNumber } from "@/shared/lib/filters";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import type { SortDirection } from "@/shared/lib/filters/types";
import type { ProfileListSortBy } from "./profileListSort";

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
  isNewHighlight,
  formatSlots,
  buildSlotsTooltip,
  buildAssistantTooltip,
} = useGameDisplay();

// Closed games are muted, and the "new" green leaves them out.
function isClosedGame(game: Game): boolean {
  return game.status === GameStatus.Closed;
}

const role = ref<RoleScope>("host");
// `search` is the debounced `searchInput`, one fetch per pause.
const searchInput = ref("");
const search = ref("");
const sortBy = ref<ProfileListSortBy>("created");
const sortOrder = ref<SortDirection>("desc");

let searchTimer: ReturnType<typeof setTimeout> | undefined;
watch(searchInput, (value) => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    search.value = value.trim();
  }, 300);
});
onBeforeUnmount(() => clearTimeout(searchTimer));

// A page as long as the reader's lists (account settings).
const { pageSize } = usePaging();

const pageNumber = computed(() => parsePageNumber(route.query.number) ?? 1);

const apiParams = computed(() => {
  const base = {
    search: search.value || undefined,
    sortBy: sortBy.value,
    sortOrder: sortOrder.value,
    number: pageNumber.value,
    size: pageSize.value,
  };
  return role.value === "host"
    ? { ...base, hostUsernames: [props.username] }
    : {
        ...base,
        playerUsername: props.username,
        // Retired characters and pending applications included.
        playerParticipation: "Any" as const,
      };
});

const envelope = ref<ListEnvelope<Game> | null>(null);

// The error line goes while the next page loads.
const { loading, error, run } = useGuardedRequest({
  message: "Не удалось загрузить игры",
  clearErrorOnStart: true,
});

function fetchGames() {
  return run(
    () => gameCatalogApi.searchGames(apiParams.value),
    (data) => {
      envelope.value = data;
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

/** Back to the first page: a page of the list being left means nothing for the next. */
function resetPage() {
  if (route.query.number) {
    const query = { ...route.query };
    delete query.number;
    router.replace({ query });
  }
}

watch(search, resetPage);

// Refetch on every change of the query; the string dedupes identical ones.
const paramsKey = computed(() => JSON.stringify(apiParams.value));
watch(paramsKey, () => fetchGames(), { immediate: true });

function setRole(next: RoleScope) {
  if (role.value === next) return;
  role.value = next;
  // The other role likely has a different page count.
  resetPage();
}

// aria-sort for drawn columns only: "created" has none, so the sort maps to "title".
const currentSort = computed<SortState | undefined>(() =>
  sortBy.value === "title"
    ? { key: "title", direction: sortOrder.value }
    : undefined,
);

// Sorted by the SortButton, as /games, not by the headers.
const sortOptions = [
  {
    value: "created",
    label: "Дата создания",
    defaultDirection: "desc" as const,
  },
  { value: "title", label: "Название", defaultDirection: "asc" as const },
];

function handleSortSelect(value: string, direction?: SortDirection) {
  sortBy.value = value === "title" ? "title" : "created";
  sortOrder.value = direction ?? "desc";
}

function handleSortOrder(order: SortDirection) {
  sortOrder.value = order;
}

// The columns of /games without the tags; in "Игрок" mode their place is the
// character status, which the server fills only for the player filter.
const columns = computed<Column[]>(() =>
  role.value === "player"
    ? [
        GAME_COLUMNS.title,
        GAME_COLUMNS.master,
        GAME_COLUMNS.status,
        {
          key: "character",
          label: "Статус персонажа",
          width: GAME_COLUMNS.tags.width,
          align: "left",
        },
        GAME_COLUMNS.reviews,
        GAME_COLUMNS.readers,
      ]
    : [
        GAME_COLUMNS.title,
        {
          ...GAME_COLUMNS.master,
          width: GAME_COLUMN_WIDTHS_WITHOUT_TAGS.master,
        },
        {
          ...GAME_COLUMNS.status,
          width: GAME_COLUMN_WIDTHS_WITHOUT_TAGS.status,
        },
        {
          ...GAME_COLUMNS.reviews,
          width: GAME_COLUMN_WIDTHS_WITHOUT_TAGS.reviews,
        },
        {
          ...GAME_COLUMNS.readers,
          width: GAME_COLUMN_WIDTHS_WITHOUT_TAGS.readers,
        },
      ],
);

// The CharacterCard caption, "В игре" where it has none (declined applications are not sent).
// The caption reads only the status and its flags, all a row's character carries.
function characterStatus(character: PlayerCharacterInfo): string {
  return characterStatusLabel(character as Character) ?? "В игре";
}

// Paging scrolls the table back into view, not the page top.
const tableRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return tableRef.value?.$el ?? null;
}
</script>

<template>
  <div class="profile-games-table">
    <!-- What does not fit beside the field goes under it, the sort and the toggle together. -->
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
      <!-- Title, unread and comments, as /games. -->
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
          }}<Tooltip :text="buildAssistantTooltip(row.assistants)" focusable>
            <span class="assistant-count">[+{{ row.assistants.length }}]</span>
          </Tooltip></template
        >
      </template>

      <!-- The status word alone; extra characters as "[+N]", the details on the game page. -->
      <template #cell-character="{ row }">
        <template v-if="row.playerCharacters?.length">
          <span class="character-status">{{
            characterStatus(row.playerCharacters[0])
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

      <!-- Reviews, as /games. -->
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

.profile-games-table
  display: flex
  flex-direction: column
  gap: $medium

// What does not fit beside the field goes to the next line whole; nothing gives up its words.
.controls
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small
  margin-bottom: 0

// The search takes the free space, as on /games.
.search
  flex: 1
  min-width: $search-field-min-width

// The sort and the toggle go down as one, side by side or one under the other.
.controls-pair
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small

// The toggle stands at the width of its segments and does not give it up.
.role-toggle
  flex: none

.game-link
  &.new-item
    color: $accent-green
    &:hover
      color: $accent-green-hover
  &.closed-item
    color: $text-muted
    &:hover
      color: $link-hover

.status-wrapper
  cursor: help

.assistant-count
  color: $text-muted
  cursor: help

// A plain muted count.
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

.readers-count
  cursor: help
</style>
