<script setup lang="ts">
import { computed, nextTick, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import {
  useZoneMarker,
  useZoneSection,
} from "@/shared/lib/composables/useZoneSection";
import {
  markArrival,
  useScrollToElement,
} from "@/shared/lib/composables/useScrollToElement";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { parsePageNumber } from "@/shared/lib/filters/utils";
import { getScrollContainer } from "@/shared/lib/scroll";
import { gameApi, RoomAccessPolicy, type DiceRollInput } from "@/entities/game";
import { RoomPostFilter, useRoomPostFilter } from "@/features/room-post-filter";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import { SvgIcon } from "@/shared/ui/Icon";
import { symbols } from "@/shared/lib/utils/icons";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { BODY_TEXT_MAX_LENGTH } from "@/shared/lib/constants/content";
import {
  MAX_POST_ATTACHMENTS,
  POST_ATTACHMENT_ACCEPT,
  describeAttachmentProblem,
  uploadPostAttachments,
} from "@/features/upload";
import { formatFileSize } from "@/shared/lib/utils/fileSize";
import { DiceComposer, GamePost } from "@/widgets/game-post";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { refusalHere } from "@/shared/lib/errors";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import Button from "@/shared/ui/Button/Button.vue";

const route = useRoute();
const router = useRouter();
const gameStore = useGameDetailsStore();
const userStore = useAuthStore();
const {
  game,
  rooms,
  currentRoom,
  roomsLoading,
  roomsError,
  posts,
  postsPaging,
  postsLoading,
  postsError,
  characters,
  isMaster,
  isAssistant,
} = storeToRefs(gameStore);
const { user } = storeToRefs(userStore);

const { filterState, apiFilter, hasActiveFilters, isFilteredView } =
  useRoomPostFilter();

const gameId = computed(() => route.params.id as string);
const roomNum = computed(() => parseInt(route.params.num as string));
// The shared Paging widget writes the page as ?number= (codebase-wide
// query-key convention) — read the same key back.
const page = computed(() => parsePageNumber(route.query.number) ?? 1);

// Resolve the room off the store's rooms list by number so it stays fresh
// after loadRooms() re-syncs (pendency mutations replace the array, which
// would otherwise orphan the currentRoom reference set inside loadPosts).
const room = computed(
  () =>
    rooms.value.find((r) => r.roomNumber === roomNum.value) ??
    currentRoom.value,
);

/**
 * The game whose list of rooms this page has seen answered. An empty list is
 * an answer too, and only this tells it from a list not asked for yet: a game
 * with no rooms lists none, and so does one whose every room is closed and
 * hidden from the reader.
 */
const listedGame = ref<string | null>(null);

/**
 * The list of the rooms of this game, asked for when the store has none. Taken
 * as an answer only when it is one for this game and no newer request is still
 * on the wire to replace it.
 */
async function ensureRooms(): Promise<void> {
  const id = gameId.value;
  if (!rooms.value.length) await gameStore.loadRooms(id);
  if (id === gameId.value && !roomsLoading.value && !roomsError.value)
    listedGame.value = id;
}

/**
 * The room the address names is not one this reader may open: closed to him,
 * kept out of his list, or not there at all. The listing of the game says so
 * (canView, or the number missing from a list that did load, an empty one
 * included) by the filter the posts would be refused by. Such a room has no
 * page: the address opens the site's own "not found", as any address that
 * leads nowhere does, and not a page of the room with a refusal on it.
 *
 * Asked of a list of this game only: on the way to another game the list of
 * the one before is still in the store for a moment.
 */
const roomRefused = computed(() => {
  const current = game.value;
  if (!current || roomsError.value) return false;
  if (!rooms.value.length && listedGame.value !== gameId.value) return false;
  const ids = [current.publicId, current.id];
  if (!ids.some((id) => id !== undefined && String(id) === gameId.value))
    return false;
  const listed = rooms.value.find((r) => r.roomNumber === roomNum.value);
  return !listed || listed.canView === false;
});

/** The site's "not found" in place of the room, at the address of the room. */
function showNotFound() {
  void router.replace({
    name: "not-found",
    params: { pathMatch: route.path.slice(1).split("/") },
    query: route.query,
    hash: route.hash,
  });
}

watch(
  roomRefused,
  (refused) => {
    if (refused) showNotFound();
  },
  { immediate: true, flush: "post" },
);

// The room IS the section here, and its name is data — meta.section cannot
// spell it. Announced to the shell, which is the only place that writes a
// heading or a tab name in this zone; the archive marker goes up with it, in
// the same quiet tag it had beside the room's own title.
useZoneSection(() => room.value?.title);
useZoneMarker(() => (room.value?.isArchived ? "архив" : undefined));

// The game-wide unread link lands here with ?scrollTo= and its page named.
const postsLoaded = computed(
  () => posts.value.length > 0 && !postsLoading.value,
);
useScrollToElement(postsLoaded);

/**
 * Position of a post in the whole room, not on the page it is shown on. The
 * server sends it; counted from the page only for the room in its own order,
 * where the two agree.
 */
function postNumber(index: number): number | undefined {
  const post = posts.value[index];
  if (post?.number) return post.number;
  if (isFilteredView.value) return undefined;
  return (postsPaging.value?.skip ?? 0) + index + 1;
}

// Paging scrolls the posts list back into view (not the page top)
const postsListRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return postsListRef.value;
}

// One target for both paging blocks, above and below the posts.
const pagingTarget = computed(() => ({
  name: "game-room",
  params: { id: game.value?.publicId || game.value?.id, num: roomNum.value },
}));

// ───────────────────────────────────────────────────────────────────────────
// Opening the room
// ───────────────────────────────────────────────────────────────────────────

/** The post id a `#post-{id}` hash names, or null. */
function hashPostId(): string | null {
  return route.hash.startsWith("#post-") ? route.hash.slice(6) || null : null;
}

/** The room the view on screen belongs to: a change of it is an entry. */
let enteredRoom: string | null = null;
/** The room already marked read by this visit. */
let markedRoomId: string | null = null;
/** A post to bring up once the page holding it is on screen. */
let pendingTarget: { postId: string; mark: boolean } | null = null;
/** The wait for where the room opens, before its page is asked for. */
const locating = ref(false);

/** Room between the top edge and a post brought up to it. */
const REVEAL_GAP = 16;

/**
 * Every step of a view goes through the shared guard: the wait for where the
 * room opens, the page itself and a reload of it. A step answered after a
 * newer one started is dropped, so a late answer can neither move the address
 * nor bring up a post on a view the reader has already left. What follows a
 * step starts inside its `apply`, in the same turn as the check, so nothing
 * newer can slip in between. Neither flag is rendered: the posts keep their
 * wait and their failure in the store, and the wait for the opening has a flag
 * of its own.
 */
const { run: runView } = useGuardedRequest({
  message: "Не удалось открыть комнату",
});

/**
 * Where a room entered without a page opens: the page a linked post stands on,
 * or for a signed-in reader the first unread post (the last one when all is
 * read). Null for a guest and on any failure: the first page, as before.
 */
async function locateOpening(): Promise<{
  page: number;
  postId: string;
  mark: boolean;
} | null> {
  // A room this reader may not open has nothing to open on.
  await ensureRooms();
  if (roomRefused.value) return null;
  const linked = hashPostId();
  if (linked) {
    const linkedPage = await gameStore.locatePostPage(linked);
    return linkedPage
      ? { page: linkedPage, postId: linked, mark: false }
      : null;
  }
  if (!user.value) return null;
  const found = await gameStore.locateFirstUnreadPost(
    gameId.value,
    roomNum.value,
  );
  return found
    ? { page: found.page, postId: found.postId, mark: found.hasUnread }
    : null;
}

function openView(): Promise<void> {
  const roomKey = `${gameId.value}/${roomNum.value}`;
  const entering = roomKey !== enteredRoom;
  enteredRoom = roomKey;
  locating.value = false;

  // Only an address without a page is placed: a filtered view starts at its
  // own first page, and the game-wide unread link names the page itself.
  if (
    entering &&
    !isFilteredView.value &&
    !route.query.number &&
    !route.query.scrollTo
  ) {
    locating.value = true;
    return runView(
      async () => ({ data: await locateOpening(), error: null }),
      (opening) => {
        locating.value = false;
        if (opening) {
          pendingTarget = { postId: opening.postId, mark: opening.mark };
          if (opening.page > 1) {
            // The address names the page from here on, so "Назад" and a
            // reload come back to it. The change of page is what loads it.
            router.replace({
              name: "game-room",
              params: route.params,
              query: { ...route.query, number: String(opening.page) },
              hash: route.hash,
            });
            return;
          }
        }
        void loadView(entering);
      },
    );
  }

  return loadView(entering);
}

/** Loads the page the address names and settles what follows from it. */
function loadView(entering = false): Promise<void> {
  return runView(
    async () => {
      // The list first, and the posts of a room it refuses are not asked
      // for: the page goes to "not found" instead (roomRefused).
      await ensureRooms();
      if (roomRefused.value) return { data: null, error: null };
      await gameStore.loadPostsByRoomNumber(
        gameId.value,
        roomNum.value,
        page.value,
        apiFilter.value,
      );
      // The posts, their wait and their failure are the store's to keep.
      return { data: null, error: null };
    },
    () => {
      if (!postsError.value && !roomRefused.value) void settleView(entering);
    },
  );
}

/** What follows from the page on screen: the post it brings up, then reading. */
async function settleView(entering: boolean) {
  const linked = entering ? hashPostId() : null;
  const target =
    pendingTarget ?? (linked ? { postId: linked, mark: false } : null);
  pendingTarget = null;

  if (isFilteredView.value) {
    leaveFilterForHashedPost();
    return;
  }
  if (target) await revealPost(target.postId, target.mark);
  markRoomRead();
}

/** Retry of the error banner: the view on screen, asked again. */
function reloadPosts() {
  return loadView();
}

// Every change of room, page or filter is a new view. Flushed after the
// render so the game shell's reset of the store, on a change of game, runs
// before this reads it.
watch(
  () =>
    JSON.stringify([gameId.value, roomNum.value, page.value, apiFilter.value]),
  () => openView(),
  { immediate: true, flush: "post" },
);

/**
 * The post at the top of the screen, a little below the edge, and marked the
 * way the forum marks the comment it brings the reader to when it is the
 * first unread one. The mark goes on the card: the card has a fill of its
 * own, which would hide a tint on the post around it.
 */
async function revealPost(postId: string, mark: boolean) {
  await nextTick();
  await new Promise((resolve) => requestAnimationFrame(resolve));
  const article = document.getElementById(`post-${postId}`);
  if (!article) return;
  const container = getScrollContainer();
  if (container) {
    const offset =
      article.getBoundingClientRect().top -
      container.getBoundingClientRect().top;
    container.scrollTo({ top: container.scrollTop + offset - REVEAL_GAP });
  } else {
    article.scrollIntoView({ block: "start" });
  }
  if (mark) markArrival(article.querySelector(".post-card") ?? article);
}

/**
 * Reading the room marks it read; a filtered or re-sorted view is not reading
 * it, and neither is an empty room. Once per room per visit.
 */
async function markRoomRead() {
  const id = currentRoom.value?.id as string | undefined;
  if (!id || !user.value || isFilteredView.value || !posts.value.length) return;
  if (markedRoomId === id) return;
  markedRoomId = id;
  // Non-critical: the counters catch up on the next visit.
  await gameApi.markRoomAsRead(id).catch(() => undefined);
}

/**
 * A post named by the hash in a filtered view goes to its own page. The
 * number of a post links to `#post-{id}`, and in a filtered view the page of
 * the post in its room is somewhere else: the number is its place in the
 * room, and the page follows from it.
 */
function leaveFilterForHashedPost() {
  if (!isFilteredView.value) return;
  const id = hashPostId();
  const post = id ? posts.value.find((p) => p.id === id) : undefined;
  if (!id || !post?.number) return;

  const ownPage = gameStore.pageOfPostNumber(post.number);
  const query: Record<string, string> = {};
  if (ownPage > 1) query.number = String(ownPage);
  // The review a link names stays named: the post opens it.
  if (typeof route.query.review === "string") query.review = route.query.review;
  pendingTarget = { postId: id, mark: false };
  router.replace({
    name: "game-room",
    params: route.params,
    query,
    hash: route.hash,
  });
}

watch(() => route.hash, leaveFilterForHashedPost);

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Постов по заданным фильтрам не найдено"
    : "В этой комнате пока нет постов",
);

// Characters back the composer's "post as" selector, the master turn
// controls and the characters filter. Loaded once per game id (independent of
// the posts page).
watch(
  gameId,
  (id) => {
    if (id) gameStore.loadCharacters(id);
  },
  { immediate: true },
);

// ───────────────────────────────────────────────────────────────────────────
// Post composer
// ───────────────────────────────────────────────────────────────────────────
const MASTER_VALUE = "__master__";
const canManageTurns = computed(() => isMaster.value || isAssistant.value);

/**
 * The characters this room lets write: an access of their own with the policy
 * "Может писать". The server admits a post as a character on exactly that, in
 * play (RoomIntentionResolver.CreatePost), and asks it in every room: an open
 * room is open to read, and writing in it still takes the access. A character
 * the room does not let write is not offered "От лица", and a player with no
 * such character gets no form, instead of a post refused after it was typed.
 */
const writingCharacterIds = computed(() => {
  const admitted = new Set<string>();
  for (const access of room.value?.accesses ?? []) {
    const id = access.character?.id;
    if (id && access.policy === RoomAccessPolicy.Full)
      admitted.add(id as string);
  }
  return admitted;
});
const myActiveCharacters = computed(() =>
  characters.value.filter(
    (c) =>
      c.status === "Active" &&
      c.author?.username === user.value?.username &&
      writingCharacterIds.value.has(c.id as string),
  ),
);
/**
 * The characters a master may mark as awaited in this room: in play, let into
 * the room by an access of their own, and with a player to wait for. The
 * server reads the one awaited off the room's accesses and refuses the rest
 * (PostPendencyService): an NPC has no player, and a character without an
 * access is not in the room. A choice it would refuse is not offered.
 */
const awaitableCharacters = computed(() => {
  const admitted = new Set<string>();
  for (const access of room.value?.accesses ?? []) {
    const id = access.character?.id;
    if (id) admitted.add(id as string);
  }
  return characters.value.filter(
    (c) =>
      c.status === "Active" &&
      !c.isNpc &&
      !!c.author &&
      admitted.has(c.id as string),
  );
});
// An NPC writes under the same access a player's character does.
const activeNpcs = computed(() =>
  characters.value.filter(
    (c) =>
      c.status === "Active" &&
      c.isNpc &&
      writingCharacterIds.value.has(c.id as string),
  ),
);

// Composer is offered to players with a character the room lets write and to
// the master / assistants (who may post as such an NPC or as themselves).
const showComposer = computed(
  () =>
    !!user.value &&
    (myActiveCharacters.value.length > 0 || canManageTurns.value),
);

// "Post as" options: own characters the room lets write first, then (for
// hosts) such NPCs and a plain master/assistant post with no character.
const postAsOptions = computed<SelectOption[]>(() => {
  const options: SelectOption[] = myActiveCharacters.value.map((c) => ({
    value: c.id as string,
    label: c.name,
  }));
  if (canManageTurns.value) {
    for (const npc of activeNpcs.value) {
      options.push({ value: npc.id as string, label: `${npc.name} (NPC)` });
    }
    options.push({ value: MASTER_VALUE, label: "От лица мастера" });
  }
  return options;
});

const selectedPostAs = ref("");
// Keep the selection valid as the options resolve / change.
watch(
  postAsOptions,
  (options) => {
    if (!options.some((o) => o.value === selectedPostAs.value)) {
      selectedPostAs.value = options[0]?.value ?? "";
    }
  },
  { immediate: true },
);

const gameText = ref("");
const metagameText = ref("");
const submitting = ref(false);
const composerError = ref<string | null>(null);

// Attachments. Chosen here, uploaded after the post exists: an attachment names
// the post it belongs to, and the post has no identifier until it is created.
const pendingFiles = ref<File[]>([]);
const attachmentError = ref<string | null>(null);
const attachmentsFull = computed(
  () => pendingFiles.value.length >= MAX_POST_ATTACHMENTS,
);

function chooseAttachments(event: Event) {
  const input = event.target as HTMLInputElement;
  attachmentError.value = null;

  for (const file of Array.from(input.files ?? [])) {
    if (attachmentsFull.value) {
      attachmentError.value = `Не больше ${MAX_POST_ATTACHMENTS} файлов`;
      break;
    }
    const problem = describeAttachmentProblem(file);
    if (problem) {
      attachmentError.value = `${file.name}: ${problem}`;
      continue;
    }
    pendingFiles.value.push(file);
  }

  // The picker keeps the chosen file selected, so picking the same one again
  // fires no change event and the second attempt looks like a dead control.
  input.value = "";
}

function removePendingFile(index: number) {
  pendingFiles.value.splice(index, 1);
  attachmentError.value = null;
}
const gameEditorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const metaEditorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

// Dice: the room decides whether rolls are allowed at all. What a roll is made
// of and how it is listed is DiceComposer's; the post takes the list as it is.
const diceEnabled = computed(() => room.value?.settings?.diceEnabled ?? false);
const diceRolls = ref<DiceRollInput[]>([]);

// After a soft-delete lands, refresh the room + posts so paging / unread
// counters stay accurate (the deleted post drops out of the list).
async function handlePostDeleted() {
  await gameStore.loadRooms(gameId.value);
  await loadView();
}

async function submitPost() {
  const text = gameText.value.trim();
  if (!text || submitting.value || !room.value?.id) return;
  submitting.value = true;
  composerError.value = null;
  attachmentError.value = null;

  const { data, error } = await gameApi.createPost(room.value.id as string, {
    characterId:
      selectedPostAs.value && selectedPostAs.value !== MASTER_VALUE
        ? selectedPostAs.value
        : undefined,
    gameText: text,
    metagameText: metagameText.value.trim() || undefined,
    diceRolls:
      diceEnabled.value && diceRolls.value.length ? diceRolls.value : undefined,
  });

  if (error) {
    submitting.value = false;
    // The line over the button of the composer, the refusals the interceptor
    // speaks for included: its plate is taken down, and the text stays in the
    // composer. An expired session and a dead connection stay with the plate.
    composerError.value = refusalHere(error, "Не удалось отправить пост");
    return;
  }

  // Two phases, and the order is forced: an attachment names the post it hangs
  // on, so the post has to exist first. That makes a partial outcome possible —
  // the post is published, a file is not — and the composer says so plainly
  // rather than clearing itself as if everything had worked. The failed files
  // stay in the list so the author can see which ones, and the post's own edit
  // mode is where they get a second try.
  const postId = data?.resource?.id as string | undefined;
  if (postId && pendingFiles.value.length > 0) {
    const { failedNames } = await uploadPostAttachments(
      postId,
      pendingFiles.value,
    );
    pendingFiles.value = pendingFiles.value.filter((f) =>
      failedNames.includes(f.name),
    );
    if (failedNames.length > 0) {
      composerError.value =
        "Пост опубликован, но файлы не приложились: " +
        `${failedNames.join(", ")}. Приложите их через правку поста.`;
    }
  }

  submitting.value = false;

  // Reset the composer and refresh the room + posts (rooms refresh clears the
  // fulfilled pendency / unread counters).
  gameText.value = "";
  metagameText.value = "";
  diceRolls.value = [];
  // Files that failed to attach belong to the post they were meant for, not to
  // the next one: left staged, they would ride along with whatever is written
  // after it.
  pendingFiles.value = [];
  gameEditorRef.value?.clear();
  metaEditorRef.value?.clear();
  await gameStore.loadRooms(gameId.value);
  await loadView();
}

// ───────────────────────────────────────────────────────────────────────────
// Master / assistant turn tracking (pendencies)
// ───────────────────────────────────────────────────────────────────────────
const pendencies = computed(() => room.value?.pendencies ?? []);
const newPendencyCharacterId = ref("");
const pendencyBusy = ref(false);

const pendencyOptions = computed<SelectOption[]>(() =>
  awaitableCharacters.value.map((c) => ({
    value: c.id as string,
    label: c.name,
  })),
);

watch(
  pendencyOptions,
  (options) => {
    if (!options.some((o) => o.value === newPendencyCharacterId.value)) {
      newPendencyCharacterId.value = options[0]?.value ?? "";
    }
  },
  { immediate: true },
);

/** A refused change of the turns, under the controls of the block. */
const turnsResult = useActionResult();

async function addPendency() {
  if (!newPendencyCharacterId.value || pendencyBusy.value || !room.value?.id)
    return;
  pendencyBusy.value = true;
  turnsResult.clear();
  try {
    const { error } = await gameApi.createPendency(room.value.id as string, {
      characterId: newPendencyCharacterId.value,
    });
    if (error) {
      turnsResult.refuse(error, "Не удалось добавить ожидание хода");
      return;
    }
    await gameStore.loadRooms(gameId.value);
  } finally {
    pendencyBusy.value = false;
  }
}

async function dismissPendency(pendencyId: string) {
  if (pendencyBusy.value) return;
  pendencyBusy.value = true;
  turnsResult.clear();
  try {
    const { error } = await gameApi.deletePendency(pendencyId);
    if (error) {
      turnsResult.refuse(error, "Не удалось снять ожидание хода");
      return;
    }
    await gameStore.loadRooms(gameId.value);
  } finally {
    pendencyBusy.value = false;
  }
}
</script>

<template>
  <div class="game-room">
    <!-- Back link -->
    <router-link
      :to="{ name: 'game', params: { id: gameId } }"
      class="back-link"
    >
      <SvgIcon name="chevronLeft" />
      Назад к игре
    </router-link>

    <!-- The room has no title of its own: the heading above says "ИГРА |
         КОМНАТА" and, for an archived room, the archive marker after it. -->
    <RoomPostFilter class="filters" :characters="characters" />

    <!-- Error — an independent banner, the way the pulse feed draws one: a
         failed refetch keeps the posts that are already on screen instead of
         replacing them with a red line. -->
    <ErrorState
      v-if="postsError"
      class="error-banner"
      :message="postsError"
      :retry="reloadPosts"
    />

    <!-- Loading -->
    <GamePostSkeleton
      v-if="(postsLoading || locating) && posts.length === 0"
      :count="3"
      :show-navigation="false"
    />

    <!-- Empty -->
    <div v-else-if="!postsError && posts.length === 0" class="posts-empty">
      <secondary-text>{{ emptyText }}</secondary-text>
    </div>

    <!-- Posts list, between the two paging blocks -->
    <template v-else>
      <PagingWithSeparators
        v-if="postsPaging"
        :paging="postsPaging"
        :to="pagingTarget"
        :use-query="true"
        query-key="number"
        :scroll-anchor="pagingAnchor"
      />

      <div ref="postsListRef" class="posts-list">
        <game-post
          v-for="(post, index) in posts"
          :key="post.id"
          :post="post"
          :data-id="post.id"
          :number="postNumber(index)"
          :search-query="filterState.search"
          editable
          @deleted="handlePostDeleted"
        />
      </div>

      <PagingWithSeparators
        v-if="postsPaging"
        :paging="postsPaging"
        :to="pagingTarget"
        :use-query="true"
        query-key="number"
        :scroll-anchor="pagingAnchor"
      />
    </template>

    <!-- The form of a post, laid out as the old site lays it out: no frame
         and no plate of a title, the captions of its fields in its words and
         order ("От лица:", "Текст сообщения:", "Броски кубиков (Очистить):",
         "Комментарий:", "Вложение", "Я жду хода от:") and "Отправить". No
         heading over it either: the form of a comment under a discussion has
         none, and the two are one form of the site. -->
    <section v-if="showComposer" class="composer">
      <div v-if="postAsOptions.length > 1" class="composer-as">
        <label class="composer-label" for="composer-post-as">От лица:</label>
        <Select
          id="composer-post-as"
          v-model="selectedPostAs"
          :options="postAsOptions"
          class="composer-as-select"
        />
      </div>

      <!-- No hint inside the fields: the captions over them say it. -->
      <span class="composer-label">Текст сообщения:</span>
      <BBCodeEditor
        ref="gameEditorRef"
        v-model="gameText"
        placeholder=""
        context="post"
        :draft-key="composerDraftKey('room', 'post', room?.id)"
        :disabled="submitting"
        :min-height="120"
        :max-length="BODY_TEXT_MAX_LENGTH"
        :is-moderator="canManageTurns"
      />

      <!-- Dice (only when the room allows them), under the text as on the old
           site. The caption is the form's, as over the editors, and carries
           the link that takes every roll back; the block under it is the
           composer of the rolls. -->
      <div v-if="diceEnabled" class="dice">
        <div class="dice-title">
          <span id="composer-dice-caption">Броски кубиков</span>
          (<button
            type="button"
            class="dice-clear"
            :disabled="submitting"
            @click="diceRolls = []"
          >
            Очистить</button
          >):
        </div>
        <DiceComposer
          v-model="diceRolls"
          aria-labelledby="composer-dice-caption"
          :disabled="submitting"
        />
      </div>

      <span class="composer-label">Комментарий:</span>
      <!-- The post's buttons and preview, the comment surface's check: the
           metagame text is saved there, and a hidden block is refused. -->
      <BBCodeEditor
        ref="metaEditorRef"
        v-model="metagameText"
        placeholder=""
        context="post"
        check-context="common"
        :draft-key="composerDraftKey('room', 'metagame', room?.id)"
        :disabled="submitting"
        :min-height="60"
        :max-height="200"
        :max-length="BODY_TEXT_MAX_LENGTH"
        :is-moderator="canManageTurns"
      />

      <!-- Attachments: chosen now, uploaded once the post exists -->
      <div class="attachments">
        <div class="attachments-title">Вложение</div>
        <ul v-if="pendingFiles.length" class="attachment-list">
          <li
            v-for="(file, i) in pendingFiles"
            :key="i"
            class="attachment-chip"
          >
            <span class="attachment-chip-label">{{ file.name }}</span>
            <span class="attachment-chip-size">{{
              formatFileSize(file.size)
            }}</span>
            <button
              type="button"
              class="attachment-chip-remove"
              aria-label="Убрать вложение"
              @click="removePendingFile(i)"
            >
              {{ symbols.close }}
            </button>
          </li>
        </ul>
        <label class="attachment-add">
          <input
            type="file"
            class="attachment-input"
            :accept="POST_ATTACHMENT_ACCEPT"
            :disabled="submitting || attachmentsFull"
            multiple
            @change="chooseAttachments"
          />
          <span class="attachment-add-label">Прикрепить файл</span>
        </label>
        <SecondaryText class="attachment-hint">
          Не больше {{ MAX_POST_ATTACHMENTS }} картинок, до 5 МБ каждая
        </SecondaryText>
        <ActionResult :text="attachmentError" />
      </div>

      <!-- Whose turn the master waits for, the last field before "Отправить"
           as on the old site. Its controls act at once and not with the post:
           a turn is awaited or dropped by its own press. -->
      <div v-if="canManageTurns" class="turns">
        <div class="turns-title">Я жду хода от:</div>
        <ul v-if="pendencies.length" class="pending-list">
          <li v-for="p in pendencies" :key="p.id" class="pending-item">
            <span class="pending-name">{{ p.characterName }}</span>
            <button
              type="button"
              class="pending-dismiss"
              :disabled="pendencyBusy"
              aria-label="Снять ожидание хода"
              @click="dismissPendency(p.id)"
            >
              {{ symbols.close }}
            </button>
          </li>
        </ul>
        <secondary-text v-else class="turns-empty">
          Сейчас ничьего хода не ждут
        </secondary-text>
        <div v-if="pendencyOptions.length" class="pending-add">
          <Select
            v-model="newPendencyCharacterId"
            :options="pendencyOptions"
            class="pending-select"
            aria-label="Персонаж, от которого ждут хода"
          />
          <Button
            type="button"
            class="pending-add-btn"
            :disabled="pendencyBusy || !newPendencyCharacterId"
            @click="addPendency"
          >
            Ждать хода
          </Button>
        </div>
        <ActionResult :result="turnsResult" />
      </div>

      <ActionResult :text="composerError" />

      <Button
        type="button"
        variant="primary"
        class="composer-submit"
        :disabled="submitting || !gameText.trim()"
        @click="submitPost"
      >
        Отправить
      </Button>
    </section>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.game-room
  min-height: $grid-step * 50

.back-link
  display: inline-flex
  align-items: center
  gap: $tiny
  margin-bottom: $medium
  color: $link
  text-decoration: none

  &:hover
    text-decoration: underline

.posts-empty
  padding: $big

.filters
  margin-bottom: $medium

.error-banner
  margin-bottom: $medium

// The posts stand a line of the text apart, as the old site's posts do (a
// line break of 20 between two of them), and the skeleton in their place
// keeps the same step (GamePostSkeleton, .in-room).
.posts-list
  display: flex
  flex-direction: column
  gap: 5 * $grid-step

// The post the room opens on wears the site's arrival pulse (.highlight-unread,
// Reset.sass) on its card. That pulse fades to transparent, which is right for
// a comment and wrong for a card with a fill of its own: the card would empty
// out and snap back when the class comes off. Here it fades into the card's own
// fill — a keyframe with no end takes the element's own value as its end.
.posts-list :deep(.post-card.highlight-unread)
  animation-name: card-arrival

@keyframes card-arrival
  from
    background-color: $bg-highlight-yellow
    box-shadow: 0 0 0 4px $bg-highlight-yellow

// ───────────────────────────────────────────────────────────────────────────
// Turn tracking: a field of the form, its caption a caption of the page
// ───────────────────────────────────────────────────────────────────────────
.turns
  margin-top: $medium

// A caption of a field of the page: +field-caption, as FormField draws it.
.turns-title
  margin-bottom: $small
  +field-caption

.turns-empty
  display: block
  margin-bottom: $small

.pending-list
  list-style: none
  display: flex
  flex-wrap: wrap
  gap: $small
  margin: 0 0 $small

.pending-item
  display: inline-flex
  align-items: center
  gap: $tiny
  padding: $tiny $small
  background-color: $bg-element
  border: 1px solid $border
  border-radius: $border-radius

.pending-name
  color: $text

.pending-dismiss
  display: inline-flex
  align-items: center
  justify-content: center
  padding: 0 $tiny
  border: none
  background: transparent
  color: $text-muted
  font-size: $font-size
  line-height: 1
  cursor: pointer
  &:hover:not(:disabled)
    color: $accent-red
  &:disabled
    opacity: $disabled-opacity
    cursor: default

// The list and its button in one line where both fit, the button under the
// list where they do not.
.pending-add
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small

// The list is as wide as its longest name, so the name chosen is never cut
// (it was 120 at every width and read "Арагорн Сле"), and never wider than the
// column. Over the field's own width of the whole line.
.pending-add .pending-select
  width: auto
  max-width: 100%
  min-width: 0

.pending-add-btn
  flex-shrink: 0

// ───────────────────────────────────────────────────────────────────────────
// Composer
// ───────────────────────────────────────────────────────────────────────────
// The form stands on the page itself, without a frame or a ground of its own,
// as the form of a comment under a discussion does.
.composer
  margin-top: $big

// The caption and the list in one line where both fit, the list under the
// caption where they do not.
.composer-as
  display: flex
  flex-wrap: wrap
  align-items: center
  gap: $small
  margin-bottom: $small

// As the list beside "Ждать хода": as wide as its longest name, so the name
// chosen is never cut (it was 120 at every width and read "Таинственны"), and
// never wider than the column. Over the field's own width of the whole line.
.composer-as .composer-as-select
  width: auto
  max-width: 100%
  min-width: 0

// A caption of a field of the page: +field-caption, as FormField draws it.
// The fields stand $medium apart, the caption $minor over its field.
.composer-label
  display: block
  margin: $medium 0 $minor
  +field-caption

// The first caption of the form starts it, and "От лица:" stands in the line
// of its list.
.composer > .composer-label:first-child
  margin-top: 0

.composer-as .composer-label
  margin: 0

.composer-submit
  margin-top: $medium

// ───────────────────────────────────────────────────────────────────────────
// Dice
// ───────────────────────────────────────────────────────────────────────────
.dice
  margin-top: $medium

// A caption of a field of the page: +field-caption, as FormField draws it,
// with the link that takes the rolls back inside its brackets.
.dice-title
  margin-bottom: $small
  +field-caption

.dice-clear
  +inline-link-button

// Attachments: a titled section, chosen files as chips with a remove control,
// and one button that adds another.
.attachments
  margin-top: $medium

// A caption of a field of the page: +field-caption, as FormField draws it.
.attachments-title
  margin-bottom: $small
  +field-caption

.attachment-list
  list-style: none
  display: flex
  flex-wrap: wrap
  gap: $small
  margin: 0 0 $small

.attachment-chip
  display: inline-flex
  align-items: center
  gap: $tiny
  padding: $tiny $small
  background-color: $bg-element
  border: 1px solid $border
  border-radius: $border-radius

.attachment-chip-label
  font-weight: bold
  color: $heading
  overflow-wrap: anywhere

.attachment-chip-size
  color: $text-muted

.attachment-chip-remove
  display: inline-flex
  align-items: center
  justify-content: center
  padding: 0 $tiny
  border: none
  background: transparent
  color: $text-muted
  font-size: $font-size
  line-height: 1
  cursor: pointer
  &:hover
    color: $accent-red

// The native picker is the click target and is covered by the label, which is
// what everything else that takes a file does.
.attachment-add
  position: relative
  display: inline-flex

.attachment-input
  position: absolute
  inset: 0
  width: 100%
  opacity: 0
  cursor: pointer

  &:disabled
    cursor: default

.attachment-add-label
  +button

.attachment-hint
  display: block
  margin-top: $tiny
</style>
