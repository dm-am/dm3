/**
 * The left column shows a game or a blog once. Its blocks stand in an order of
 * seniority, and a block leaves out what a block above it shows: "Мои игры",
 * "Набор игроков", "Активные игры", "Завершенные игры"; "Мои блоги", "Активные
 * блоги". A block left with nothing is drawn as an empty one.
 */
import { computed, watch } from "vue";
import {
  GameParticipation,
  SIDEBAR_GAME_ROWS,
  useGamesStore,
  type GameRef,
} from "@/entities/game";
import { SIDEBAR_BLOG_ROWS, useBlogsStore } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { MAX_API_PAGE_SIZE } from "@/shared/lib/composables/usePaging";

type Row = { id: string };
type Ids = ReadonlySet<string>;

const idsOf = (...lists: (readonly Row[] | null | undefined)[]): Ids =>
  new Set(lists.flatMap((list) => (list ?? []).map((row) => row.id)));

/** The rows a block draws: the ones a block above shows left out, at most `limit`. */
function rowsBelow<T extends Row>(
  items: readonly T[] | null,
  above: Ids,
  limit = Infinity,
): T[] | null {
  if (!items) return null;
  return items.filter((row) => !above.has(row.id)).slice(0, limit);
}

// Authority covers the master and the assistants; Owner is the master alone.
const OWNER_FLAGS: GameParticipation[] = [
  GameParticipation.Owner,
  GameParticipation.Authority,
];

const hasRole = (game: GameRef, flag: GameParticipation) =>
  game.participation?.includes(flag) ?? false;
const hasAnyOwnerRole = (game: GameRef) =>
  game.participation?.some((p) => OWNER_FLAGS.includes(p)) ?? false;

/**
 * The groups of "Мои игры" in their order: the curated with "~ ", the led, the
 * played, the read. A game the viewer leads is his own, never the curated one.
 */
const OWN_GAME_GROUPS = [
  {
    key: "mentor",
    prefix: "~ ",
    has: (g: GameRef) =>
      hasRole(g, GameParticipation.Moderator) && !hasAnyOwnerRole(g),
  },
  { key: "owned", prefix: undefined, has: hasAnyOwnerRole },
  {
    key: "playing",
    prefix: undefined,
    has: (g: GameRef) => hasRole(g, GameParticipation.Player),
  },
  {
    key: "reading",
    prefix: undefined,
    has: (g: GameRef) => hasRole(g, GameParticipation.Reader),
  },
];

/** "Мои игры": a game once, in the first group it fits, empty groups left out. */
function ownGameGroups(games: readonly GameRef[]) {
  const groupOf = (game: GameRef) =>
    OWN_GAME_GROUPS.findIndex((group) => group.has(game));
  return OWN_GAME_GROUPS.map(({ key, prefix }, at) => ({
    key,
    prefix,
    games: games.filter((game) => groupOf(game) === at),
  })).filter((group) => group.games.length > 0);
}

/** The game lists of the column, each without the games of the blocks above it. */
export function useLeftSidebarGames() {
  const auth = useAuthStore();
  const store = useGamesStore();

  const mine = computed<readonly GameRef[]>(() =>
    auth.user ? (store.participatingGames ?? []) : [],
  );
  const own = computed(() => ownGameGroups(mine.value));
  const aboveRecruiting = computed(() =>
    idsOf(...own.value.map((group) => group.games)),
  );

  const recruiting = computed(() =>
    rowsBelow(
      store.recruitingGames,
      aboveRecruiting.value,
      SIDEBAR_GAME_ROWS.recruiting,
    ),
  );
  const aboveActive = computed(
    () => new Set([...aboveRecruiting.value, ...idsOf(recruiting.value)]),
  );

  const active = computed(() =>
    rowsBelow(store.activeGames, aboveActive.value, SIDEBAR_GAME_ROWS.active),
  );
  // "Активные игры" is the guest's block alone: for a viewer it shows nothing.
  const aboveFinished = computed(
    () =>
      new Set([
        ...aboveActive.value,
        ...(auth.user ? [] : idsOf(active.value)),
      ]),
  );

  const finished = computed(() =>
    rowsBelow(
      store.finishedGames,
      aboveFinished.value,
      SIDEBAR_GAME_ROWS.finished,
    ),
  );

  return {
    own,
    aboveRecruiting,
    recruiting,
    aboveActive,
    active,
    aboveFinished,
    finished,
  };
}

/** The blog lists of the column, each without the blogs of the blocks above it. */
export function useLeftSidebarBlogs() {
  const auth = useAuthStore();
  const store = useBlogsStore();

  // "Мои блоги" draws every blog of the viewer.
  const aboveActive = computed(() =>
    auth.user ? idsOf(store.participatingBlogs) : idsOf(),
  );

  const active = computed(() =>
    rowsBelow(store.activeBlogs, aboveActive.value, SIDEBAR_BLOG_ROWS),
  );

  return { aboveActive, active };
}

/**
 * A block keeps its length when the blocks above take some of its rows: a
 * full page that came short is asked again, wider by every row they could take.
 */
export function useFilledRows<T extends Row>(list: {
  items: () => readonly T[] | null;
  rows: () => readonly T[] | null;
  above: () => Ids;
  limit: number;
  take: () => number;
  widen: (take: number) => unknown;
}) {
  watch(
    () => [list.items(), list.rows(), list.above().size] as const,
    ([items, rows, aboveCount]) => {
      // Not read yet, full, or the server has no more than it sent.
      if (!items || !rows || rows.length >= list.limit) return;
      if (items.length < list.take()) return;
      const wanted = Math.min(list.limit + aboveCount, MAX_API_PAGE_SIZE);
      if (wanted > list.take()) void list.widen(wanted);
    },
    { immediate: true },
  );
}
