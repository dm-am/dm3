<script setup lang="ts">
/**
 * MessageSearchPanel — inline unified search over the global chat, the user's
 * chats/DMs and readable game rooms.
 *
 * Layered over the chat feed (the live feed stays mounted underneath). Carries
 * a query input, a scope selector (Везде / Глобальный чат / ЛС / Игра, mapping
 * to the backend `in:` param), a sort toggle (По дате / Лучшее совпадение), and
 * a keyset-paginated result list whose rows jump to the message/post in context.
 *
 * Jump-to-context:
 *   - global message -> handled by the parent page (reuses the global-chat
 *     store's navigateToMessage over the live feed) via @jump-global.
 *   - chat/DM message -> routes to the messenger chat with ?msg=<id>.
 *   - game post       -> resolves room+page (resolvePostJump) and routes to the
 *     game room with scrollTo.
 */
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { FilterSearchInput } from "@/shared/ui/Filters";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";
import { symbols } from "@/shared/lib/utils/icons";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useAuthStore } from "@/shared/stores";
import { messagingApi } from "@/entities/message";
import type { ChatId } from "@/entities/message";
import { gameApi } from "@/entities/game";
import { useMessageSearchStore, scopeNeedsTarget } from "../model/searchStore";
import type {
  MessageSearchResult,
  SearchScopeKind,
  SearchSort,
} from "../model/types";
import { resolvePostJump } from "../lib/resolvePostJump";

const emit = defineEmits<{
  close: [];
  "jump-global": [messageId: string];
}>();

const router = useRouter();
const store = useMessageSearchStore();
const {
  query,
  scope,
  sort,
  results,
  hasMore,
  loading,
  loadingMore,
  error,
  hasSearched,
  canSearch,
} = storeToRefs(store);
const { user: currentUser } = storeToRefs(useAuthStore());

const rootRef = ref<HTMLElement | null>(null);

// ─────────────────────────────────────────────────────────────
// Query input — debounced auto-search, immediate on Enter.
// ─────────────────────────────────────────────────────────────
const DEBOUNCE_MS = 300;
let debounceTimer: ReturnType<typeof setTimeout> | null = null;

function scheduleSearch() {
  if (debounceTimer) clearTimeout(debounceTimer);
  debounceTimer = setTimeout(() => {
    debounceTimer = null;
    store.search();
  }, DEBOUNCE_MS);
}

function onQueryInput(value: string) {
  query.value = value;
  if (!value.trim()) {
    store.reset();
    return;
  }
  scheduleSearch();
}

function onQueryKeydown(event: KeyboardEvent) {
  if (event.key === "Enter") {
    event.preventDefault();
    if (debounceTimer) {
      clearTimeout(debounceTimer);
      debounceTimer = null;
    }
    store.search();
  }
}

// ─────────────────────────────────────────────────────────────
// Scope control
// ─────────────────────────────────────────────────────────────
const scopeOptions: { value: SearchScopeKind; label: string }[] = [
  { value: "all", label: "Везде" },
  { value: "global", label: "Глобальный чат" },
  { value: "dm", label: "ЛС" },
  { value: "game", label: "Игра" },
];

const needsTarget = computed(() => scopeNeedsTarget(scope.value));

function selectScope(kind: SearchScopeKind) {
  if (scope.value.kind === kind) return;
  // Keep a previously picked target if re-selecting the same kind; otherwise
  // start the new scope fresh (no target).
  store.setScope({ kind });
  if (kind === "dm") void ensureChats();
  if (kind === "game") void ensureGames();
  if (canSearch.value) store.search();
  else store.reset();
}

// dm/game target pickers — loaded lazily on first use.
type TargetOption = { id: string; label: string };
const chatTargets = ref<TargetOption[]>([]);
const gameTargets = ref<TargetOption[]>([]);
const targetsLoading = ref(false);

function counterpartOf(participants: { username: string }[]): string {
  const me = currentUser.value?.username?.toLowerCase();
  const other = participants.find((p) => p.username?.toLowerCase() !== me);
  return other?.username ?? participants[0]?.username ?? "";
}

async function ensureChats() {
  if (chatTargets.value.length > 0) return;
  targetsLoading.value = true;
  try {
    const { data } = await messagingApi.getChats({ number: 1, take: 50 });
    chatTargets.value = (data?.resources ?? []).map((c) => {
      const title = (c.title as unknown as string | null) ?? "";
      const participants = (c.participants ?? []) as unknown as {
        username: string;
      }[];
      return {
        id: c.id as unknown as string,
        label: title.trim() ? title : counterpartOf(participants),
      };
    });
  } finally {
    targetsLoading.value = false;
  }
}

async function ensureGames() {
  if (gameTargets.value.length > 0) return;
  targetsLoading.value = true;
  try {
    const { data } = await gameApi.getParticipatingGames();
    gameTargets.value = (data?.resources ?? []).map((g) => ({
      id: g.id as unknown as string,
      label: g.title,
    }));
  } finally {
    targetsLoading.value = false;
  }
}

const targetOptions = computed(() =>
  scope.value.kind === "dm" ? chatTargets.value : gameTargets.value,
);

function onTargetChange(event: Event) {
  const id = (event.target as HTMLSelectElement).value;
  const option = targetOptions.value.find((o) => o.id === id);
  store.setScope({
    kind: scope.value.kind,
    targetId: id || undefined,
    targetLabel: option?.label,
  });
  if (canSearch.value) store.search();
  else store.reset();
}

// ─────────────────────────────────────────────────────────────
// Sort control
// ─────────────────────────────────────────────────────────────
const sortOptions: { value: SearchSort; label: string }[] = [
  { value: "date", label: "По дате" },
  { value: "best", label: "Лучшее совпадение" },
];

function selectSort(value: SearchSort) {
  if (sort.value === value) return;
  store.setSort(value);
  if (canSearch.value) store.search();
}

// ─────────────────────────────────────────────────────────────
// Result labels — Russian UI strings assembled client-side. Direct-chat
// counterparts are resolved on demand (the search row carries no author).
// ─────────────────────────────────────────────────────────────
const counterparts = ref<Record<string, string>>({});

async function ensureCounterpart(chatId: string) {
  if (counterparts.value[chatId] !== undefined) return;
  counterparts.value[chatId] = ""; // mark in-flight to dedupe
  const { data } = await messagingApi.getChat(chatId as unknown as ChatId);
  const participants =
    ((data?.participants ?? []) as unknown as { username: string }[]) ?? [];
  counterparts.value = {
    ...counterparts.value,
    [chatId]: counterpartOf(participants),
  };
}

function sourceLabel(result: MessageSearchResult): string {
  if (result.sourceType === "global") return "Глобальный чат";
  if (result.sourceType === "game") {
    return result.sourceTitle ? `Игра ${result.sourceTitle}` : "Игра";
  }
  // chat: group chats carry a title; direct chats derive the counterpart.
  if (result.sourceTitle && result.sourceTitle.trim()) {
    return result.sourceTitle;
  }
  const name = counterparts.value[result.sourceId];
  return name ? `ЛС с ${name}` : "Личные сообщения";
}

// Resolve counterpart usernames for any direct-chat rows currently shown.
watch(results, (rows) => {
  for (const r of rows) {
    if (r.sourceType === "chat" && !(r.sourceTitle && r.sourceTitle.trim())) {
      void ensureCounterpart(r.sourceId);
    }
  }
});

/**
 * Flatten the date-ordered results into a stream that inserts a group header
 * whenever the source label changes from the previous row — grouped labels
 * without disturbing the newest-first ordering.
 */
type Row =
  | { kind: "header"; key: string; label: string }
  | { kind: "result"; key: string; result: MessageSearchResult; label: string };

const rows = computed<Row[]>(() => {
  const out: Row[] = [];
  let lastLabel: string | null = null;
  for (const result of results.value) {
    const label = sourceLabel(result);
    if (label !== lastLabel) {
      out.push({ kind: "header", key: `h-${result.id}`, label });
      lastLabel = label;
    }
    out.push({ kind: "result", key: result.id, result, label });
  }
  return out;
});

// ─────────────────────────────────────────────────────────────
// Jump-to-context
// ─────────────────────────────────────────────────────────────
const resolvingId = ref<string | null>(null);

async function jumpTo(result: MessageSearchResult) {
  if (resolvingId.value) return;
  if (result.sourceType === "global") {
    emit("jump-global", result.id);
    emit("close");
    return;
  }
  if (result.sourceType === "chat") {
    router.push({
      name: "chat",
      params: { id: result.sourceId },
      query: { msg: result.id },
    });
    emit("close");
    return;
  }
  // game post — resolve room + page before navigating.
  resolvingId.value = result.id;
  try {
    const target = await resolvePostJump(result.id);
    if (target) {
      router.push({
        name: "game-room",
        params: { id: result.sourceId, num: target.roomNumber },
        query: {
          ...(target.page > 1 ? { page: String(target.page) } : {}),
          scrollTo: target.postId,
        },
      });
    } else {
      // Couldn't locate the post — land on the game as a graceful fallback.
      router.push({ name: "game", params: { id: result.sourceId } });
    }
    emit("close");
  } finally {
    resolvingId.value = null;
  }
}

// ─────────────────────────────────────────────────────────────
// Lifecycle — focus the input, close on Escape.
// ─────────────────────────────────────────────────────────────
function onKeydown(event: KeyboardEvent) {
  if (event.key === "Escape") {
    emit("close");
  }
}

onMounted(() => {
  document.addEventListener("keydown", onKeydown);
  nextTick(() => {
    rootRef.value?.querySelector<HTMLInputElement>("input")?.focus();
  });
});

onUnmounted(() => {
  document.removeEventListener("keydown", onKeydown);
  if (debounceTimer) clearTimeout(debounceTimer);
});
</script>

<template>
  <div ref="rootRef" class="search-panel" role="search">
    <!-- Control bar -->
    <div class="search-head">
      <div class="search-row">
        <FilterSearchInput
          :model-value="query"
          placeholder="Поиск сообщений и постов"
          aria-label="Поисковый запрос"
          @update:model-value="onQueryInput"
          @keydown="onQueryKeydown"
        />
        <button
          type="button"
          class="search-close"
          aria-label="Закрыть поиск"
          title="Закрыть"
          @click="emit('close')"
        >
          {{ symbols.close }}
        </button>
      </div>

      <div class="search-controls">
        <SegmentedControl
          :model-value="scope.kind"
          :options="scopeOptions"
          ariaLabel="Область поиска"
          @update:model-value="selectScope"
        />

        <div class="sort-control">
          <template v-for="(opt, i) in sortOptions" :key="opt.value">
            <span v-if="i > 0" class="sort-sep" aria-hidden="true">{{
              " | "
            }}</span>
            <button
              type="button"
              class="sort-link"
              :class="{ active: sort === opt.value }"
              :aria-pressed="sort === opt.value"
              @click="selectSort(opt.value)"
            >
              {{ opt.label }}
            </button>
          </template>
        </div>
      </div>

      <!-- dm/game target picker -->
      <div
        v-if="scope.kind === 'dm' || scope.kind === 'game'"
        class="target-row"
      >
        <label class="target-label">
          {{ scope.kind === "dm" ? "Чат:" : "Игра:" }}
          <select
            class="target-select"
            :value="scope.targetId ?? ''"
            @change="onTargetChange"
          >
            <option value="">
              {{ scope.kind === "dm" ? "Выберите чат" : "Выберите игру" }}
            </option>
            <option v-for="o in targetOptions" :key="o.id" :value="o.id">
              {{ o.label }}
            </option>
          </select>
        </label>
        <secondary-text v-if="targetsLoading" class="target-loading"
          >Загрузка…</secondary-text
        >
      </div>
    </div>

    <!-- Results -->
    <div class="search-results" aria-live="polite">
      <secondary-text v-if="needsTarget" class="search-state">
        {{
          scope.kind === "dm"
            ? "Выберите чат для поиска"
            : "Выберите игру для поиска"
        }}
      </secondary-text>

      <secondary-text
        v-else-if="!canSearch && !hasSearched"
        class="search-state"
      >
        Введите запрос, чтобы искать по сообщениям и постам
      </secondary-text>

      <secondary-text v-else-if="loading" class="search-state">
        Идет поиск…
      </secondary-text>

      <div
        v-else-if="error && results.length === 0"
        class="search-state search-error"
        role="alert"
      >
        <secondary-text>{{ error }}</secondary-text>
        <button type="button" class="retry-button" @click="store.search()">
          Повторить
        </button>
      </div>

      <secondary-text
        v-else-if="hasSearched && results.length === 0"
        class="search-state"
      >
        Ничего не найдено
      </secondary-text>

      <template v-else>
        <template v-for="row in rows" :key="row.key">
          <div v-if="row.kind === 'header'" class="result-group">
            {{ row.label }}
          </div>
          <button
            v-else
            type="button"
            class="result-card"
            :disabled="resolvingId === row.result.id"
            @click="jumpTo(row.result)"
          >
            <span class="result-meta">
              <span v-if="row.result.author" class="result-author">{{
                row.result.author.username
              }}</span>
              <span v-if="row.result.author" class="result-sep">{{
                " | "
              }}</span>
              <span class="result-time">{{
                formatDateFull(row.result.createdUtc)
              }}</span>
              <span
                v-if="resolvingId === row.result.id"
                class="result-sep"
                aria-hidden="true"
                >{{ " | " }}</span
              >
              <span v-if="resolvingId === row.result.id" class="result-time"
                >переход…</span
              >
            </span>
            <span class="result-snippet">{{ row.result.snippet }}</span>
          </button>
        </template>

        <!-- Load more -->
        <div v-if="hasMore" class="load-more-row">
          <secondary-text v-if="error" class="load-more-error">
            {{ error }}
          </secondary-text>
          <button
            type="button"
            class="load-more-button"
            :disabled="loadingMore"
            @click="store.loadMore()"
          >
            {{ loadingMore ? "Загрузка…" : "Показать еще" }}
          </button>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"
@import "src/assets/styles/ZIndex"
@import "src/assets/styles/Animations"

// Overlay filling the chat frame, layered over the live feed which stays
// mounted underneath. Fades/settles in on the site's one reveal idiom.
.search-panel
  position: absolute
  inset: 0
  z-index: $z-dropdown
  display: flex
  flex-direction: column
  background-color: $bg-page
  animation: search-in $expand-duration $expand-easing

@keyframes search-in
  from
    opacity: 0
    transform: translateY(-$small)
  to
    opacity: 1
    transform: translateY(0)

// Control bar — dashed bottom border hands over to the results list, matching
// the events strip. No rounding on the strip itself (informational surface).
.search-head
  flex-shrink: 0
  display: flex
  flex-direction: column
  gap: $small
  padding: $small $medium
  background-color: $bg-element
  border-bottom: 1px dashed $border

.search-row
  display: flex
  align-items: center
  gap: $small

  :deep(.search-container)
    flex: 1 1 auto

.search-close
  flex-shrink: 0
  display: flex
  align-items: center
  justify-content: center
  width: 32px
  height: 32px
  padding: 0
  border: none
  border-radius: $border-radius
  background: transparent
  color: $text-muted
  font-size: 1.2rem
  line-height: 1
  cursor: pointer
  &:hover
    background-color: $bg-element-accent
    color: $text

.search-controls
  display: flex
  align-items: center
  justify-content: space-between
  gap: $medium
  flex-wrap: wrap

// Scope: the shared SegmentedControl (canonical metrics — this panel's
// local copy had drifted: ~26px tall, weight 600, bg-filling hover).

// Sort: quiet " | "-separated link controls (bar idiom).
.sort-control
  display: inline-flex
  align-items: center
  font-size: $secondary-font-size

.sort-sep
  color: $text-muted

.sort-link
  +inline-link-button
  &
    font-size: $secondary-font-size
    white-space: nowrap
    color: $text-muted
  &:hover:not(:disabled)
    color: $link
  &.active
    color: $link
    font-weight: 600
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: 2px

.target-row
  display: flex
  align-items: center
  gap: $small

.target-label
  display: inline-flex
  align-items: center
  gap: $minor
  font-size: $secondary-font-size
  color: $text-muted

.target-select
  +input
  &
    max-width: 260px

.target-loading
  white-space: nowrap

// Results list — plain surface, square (informational). Scrolls on its own.
.search-results
  flex: 1 1 auto
  overflow-y: auto
  padding: $small 0
  min-height: 200px

.search-state
  display: flex
  flex-direction: column
  align-items: center
  justify-content: center
  gap: $small
  text-align: center
  padding: $big $medium

.search-error
  color: $accent-red

.retry-button
  +button

// Group header — quiet source label above a run of same-source results.
.result-group
  padding: $small $medium $tiny
  color: $text-muted
  font-size: $secondary-font-size
  font-weight: 600

// A result row is a real control (jumps to context): full-width, left-aligned,
// hover-highlighted. Text inside stays selectable.
.result-card
  display: flex
  flex-direction: column
  gap: $minor
  width: 100%
  padding: $small $medium
  border: none
  background: transparent
  text-align: left
  cursor: pointer
  user-select: text
  &:hover:not(:disabled)
    background-color: $bg-element
  &:disabled
    cursor: default
    opacity: 0.7
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: -2px

.result-meta
  font-size: $secondary-font-size
  color: $text-muted

.result-author
  color: $text

.result-sep
  color: $text-muted

.result-snippet
  color: $text
  word-break: break-word
  overflow-wrap: break-word

.load-more-row
  display: flex
  flex-direction: column
  align-items: center
  gap: $small
  padding: $medium

.load-more-error
  color: $accent-red

.load-more-button
  +button
</style>
