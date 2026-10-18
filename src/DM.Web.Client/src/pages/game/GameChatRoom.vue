<script setup lang="ts">
// Chat-type game room (RoomType.Chat): a message-based, cursor-paginated
// out-of-character room. Resolves the Room by its number from the store, then
// drives the message stream off the chat gameApi keyed by that room's id
// (the chat-rooms/{id}/messages endpoints resolve the room's linked chatId
// server-side). Rendering reuses the shared ChatMessage widget; sending
// reuses the BBCodeEditor.
import { computed, ref, watch, nextTick, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  gameApi,
  RoomAccessPolicy,
  RoomAccessType,
  type RoomAccess,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { LIVE_TAIL_POLL_INTERVAL_MS } from "@/shared/api/client";
import type { Message, CursorPaging } from "@/shared/api/models/common";
import {
  groupMessagesWithSeparators,
  isDateSeparator,
  isUserOnline,
  type MessageOrSeparator,
} from "@/shared/lib/utils/chat";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";
import { isNearBottom } from "@/shared/lib/utils/liveTail";
import { ChatMessage } from "@/widgets/chat-message";
import { LoginPrompt } from "@/features/auth";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import RefusalNote from "@/shared/ui/Layout/RefusalNote.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { ChatMessageSkeleton, CommentSkeleton } from "@/shared/ui/Skeleton";
import {
  useZoneMarker,
  useZoneSection,
} from "@/shared/lib/composables/useZoneSection";
import Button from "@/shared/ui/Button/Button.vue";

const PAGE_SIZE = 50;
const MAX_MESSAGE_HEIGHT = 300;

const route = useRoute();
const router = useRouter();
const gameStore = useGameDetailsStore();
const { game, rooms, roomsLoading, roomsError, isMaster, isAssistant } =
  storeToRefs(gameStore);
const { user } = storeToRefs(useAuthStore());
const { isCompactLayout } = storeToRefs(useUiStore());

const gameId = computed(() => route.params.id as string);
const roomNum = computed(() => parseInt(route.params.num as string));

// The chat room resolved off the store's rooms list by its number. Its id is
// the chat-room identifier used by every chat message endpoint.
const room = computed(
  () => rooms.value.find((r) => r.roomNumber === roomNum.value) ?? null,
);
const chatRoomId = computed(() => (room.value?.id as string) ?? null);

/**
 * The room the address names is in the list and open to the viewer. The list
 * answers canView false for a closed room he may not enter, a guest included.
 */
const roomOpen = computed(() => !!room.value && room.value.canView !== false);

/**
 * A guest reads an open room, as everybody who sees the game does, and no
 * closed one: a closed room is read only by the people it was opened to, all
 * of them signed in. A closed room the list still opens to a guest is the list
 * of the viewer whose session has just run out.
 */
const guestReads = computed(
  () =>
    !user.value &&
    roomOpen.value &&
    room.value?.access !== RoomAccessType.Private,
);

/** The viewer is one the room's messages are read for. */
const readsRoom = computed(
  () => roomOpen.value && (!!user.value || guestReads.value),
);

/**
 * The game whose list of rooms this page has seen answered. An empty list is
 * an answer too, and only this tells it from a list not asked for yet.
 */
const listedGame = ref<string | null>(null);

async function ensureRooms() {
  const id = gameId.value;
  if (!rooms.value.length) await gameStore.loadRooms(id);
  if (id === gameId.value && !roomsLoading.value && !roomsError.value)
    listedGame.value = id;
}

/**
 * The server turned the room's messages away from the viewer (403, 404): the
 * room is not within his reach, whatever the list said of it when it was
 * loaded (the room was closed while he read it, or his access was taken
 * away).
 */
const serverRefused = ref(false);

/**
 * The room the address names is not one this reader may open: closed to him,
 * kept out of his list, or not there at all. Such a room has no page, as a
 * room of posts has none (GameRoom.vue): the address opens the site's own "not
 * found", as any address that leads nowhere does, and not a page of the room
 * with a refusal on it.
 *
 * Asked of a list of this game only: on the way to another game the list of
 * the one before is still in the store for a moment.
 */
const roomRefused = computed(() => {
  if (serverRefused.value) return true;
  const current = game.value;
  if (!current || roomsError.value) return false;
  if (!rooms.value.length && listedGame.value !== gameId.value) return false;
  const ids = [current.publicId, current.id];
  if (!ids.some((id) => id !== undefined && String(id) === gameId.value))
    return false;
  return !roomOpen.value;
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
// heading or a tab name in this zone: the name stands once, in "ИГРА |
// КОМНАТА", as for a room of posts, and the archive marker goes up with it.
useZoneSection(() => room.value?.title);
useZoneMarker(() => (room.value?.isArchived ? "архив" : undefined));

// ───────────────────────────────────────────────────────────────────────────
// Message stream (cursor pagination)
// ───────────────────────────────────────────────────────────────────────────
const messages = ref<Message[]>([]);
const paging = ref<CursorPaging | null>(null);
const loading = ref(false);
const loadingOlder = ref(false);
const sending = ref(false);
const error = ref<string | null>(null);
/**
 * The server read the room's messages to the signed-in only (401): a session
 * that ran out (the viewer is gone from the store by the time the answer is
 * read), or a guest. The page then invites him to sign in to read it.
 */
const denied = ref(false);

/**
 * The load that is current goes through the shared guard. A load started while
 * another is in flight (the viewer changed, the address moved to another room)
 * makes the earlier one's answer stale, and a stale answer applied over the
 * current one put a refusal over a room the viewer may read. The fetcher never
 * reports a failure to the guard: the page keeps its own wait, failure and
 * refusal, so neither flag of the composable is rendered, and everything that
 * follows from the answer happens in `apply`, which a stale answer never
 * reaches.
 */
const { run: runLoad } = useGuardedRequest({
  message: "Не удалось загрузить сообщения",
});

/**
 * Takes a failed read for the room refusing the viewer, where it is one, and
 * answers whether it was. A 401 says the room is read to the signed-in: the
 * guest is invited in. Since the open room is open to read, a signed-in viewer
 * is turned away only by a closed one, and the server answers him 404: the
 * room is not within his reach (RoomAvailable) before any rule of the room is
 * asked. The room is in the list the page resolved it from, so a 404 here is
 * that refusal, not a failure, and the room goes the way of one the list
 * closes (roomRefused). The one viewer it reaches in an open room is the judge
 * of premoderation, whose rank opens the list and the page of a game waiting
 * for his verdict and not its history (RoomIntentionResolver).
 */
function takeRefusal(status: number | undefined): boolean {
  if (status === 401) {
    denied.value = true;
    return true;
  }
  if (status === 403 || status === 404) {
    serverRefused.value = true;
    return true;
  }
  return false;
}

const hasMoreBefore = computed(() => paging.value?.hasPrev ?? false);

const messagesWithSeparators = computed((): MessageOrSeparator[] =>
  groupMessagesWithSeparators(messages.value),
);

const newMessage = ref("");
const messagesContainer = ref<HTMLElement | null>(null);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

/**
 * A grant of this room that is the viewer's own and lets him write: a reader
 * row in his name, or a row of a character of his that is still in play. The
 * policy is what separates a seat from a voice, and a character that has left
 * the game keeps its rows without writing through them.
 */
function writesThrough(access: RoomAccess): boolean {
  if (access.policy !== RoomAccessPolicy.Full) return false;
  const me = user.value?.username;
  if (!me) return false;
  const character = access.character;
  if (character)
    return character.status === "Active" && character.author?.username === me;
  return access.user?.username === me;
}

/**
 * Who writes here. Openness is about reading: an open room is read by
 * everybody who sees the game, and written in, like a private one, by the
 * master, the assistants and the people the room lets write. The server asks
 * the same before it takes a message (RoomIntentionResolver.SendMessage).
 */
const mayWrite = computed(
  () =>
    isMaster.value ||
    isAssistant.value ||
    (room.value?.accesses ?? []).some(writesThrough),
);

// Sending needs the same admission reading does: a viewer the server turned
// away is not given an editor that fails on submit. Reading is not writing,
// though, and a reader the room does not let write gets the plaque saying who
// does instead of an editor the server would refuse.
const canSend = computed(
  () => !!user.value && !denied.value && roomOpen.value && mayWrite.value,
);

/**
 * In place of the room, the one thing a guest can do about it: a closed room
 * the list still opens (the list of the viewer whose session has just run
 * out), or one the server reads to the signed-in only.
 */
const invitesSignIn = computed(
  () => roomOpen.value && (denied.value || (!user.value && !guestReads.value)),
);

// The frame of the messages stands for every room but one a guest is invited
// to sign in to, which is one plaque, and one that has no page here, which
// gives way to "not found".
const showsFeed = computed(() => !invitesSignIn.value && !roomRefused.value);

// The plaque stands only once the room and the game are in, so that a master
// does not see it blink before his role arrives, and not while the messages
// load, since the room may yet turn out to be one he may not read at all.
// Nor under a load that failed: there is nothing on screen it would be about.
// A guest reading an open room gets it too, and not "Войдите, чтобы ...":
// signing in lets nobody write here by itself, and where writing belongs to
// some, the guest is told who they are, as under a discussion read-only to
// those outside the game.
const showsWhoWrites = computed(
  () =>
    showsFeed.value &&
    !error.value &&
    !loading.value &&
    roomOpen.value &&
    !!game.value &&
    !mayWrite.value,
);

function scrollToBottom() {
  nextTick(() => {
    const el = messagesContainer.value;
    if (el) el.scrollTop = el.scrollHeight;
  });
}

// Track latest activity per username for the online dot in ChatMessage.
const latestActivityByUsername = computed(() => {
  const map = new Map<string, string>();
  for (const msg of messages.value) {
    const username = msg.author?.username;
    const activity = msg.author?.lastActivityUtc;
    if (!username || !activity) continue;
    const existing = map.get(username);
    if (!existing || activity > existing) map.set(username, activity);
  }
  return map;
});
function isOnline(author: Message["author"]): boolean {
  if (!author?.username) return false;
  return isUserOnline(latestActivityByUsername.value.get(author.username));
}

function loadInitial(): Promise<void> {
  loading.value = true;
  error.value = null;
  // A refusal belongs to the room it was given for. Left standing, it shut the
  // next room the reader opened and the retry button of this one: the request
  // went out, the messages came back, and the screen went on showing the
  // refusal because nothing ever put this back.
  denied.value = false;
  serverRefused.value = false;
  messages.value = [];
  paging.value = null;

  return runLoad(
    async () => {
      await ensureRooms();
      // A room the list does not open to the viewer, or does not hold, is not
      // asked for: it has no page, and the address goes to "not found"
      // (roomRefused). A guest of a closed room is not asked for either: the
      // one answer he could get is a refusal, and a 401 the client takes for a
      // session that ran out, so "Сессия истекла, войдите снова" would stand
      // over him. He is invited to sign in instead, and the room is read the
      // moment he does (useViewerChange below).
      if (!roomOpen.value || !readsRoom.value)
        return { data: null, error: null };
      const answer = await gameApi.getChatMessages(
        chatRoomId.value,
        undefined,
        PAGE_SIZE,
      );
      return { data: answer, error: null };
    },
    (answer) => {
      loading.value = false;
      if (!answer) {
        // loadRooms hands the shell and this page the same request, so the
        // wait above means the list is in, unless a reset dropped its answer on
        // purpose; the room is read the moment it resolves (watch below). A
        // list that failed is said where the messages stand, with the retry
        // that asks for it again: "not found" would blame the address for a
        // failed request.
        if (!roomOpen.value && roomsError.value) error.value = roomsError.value;
        return;
      }
      const { data, error: err } = answer;
      if (err) {
        // Openness is about reading: an open chat room is read by everybody
        // who sees the game, a closed one by the master, the assistants and
        // whoever it was opened to, as a reader or through a character
        // (RoomIntentionResolver.ViewMessages). So a refusal here is a closed
        // room's rule working, not a failure. Saying "Не удалось загрузить
        // сообщения" blamed the site for its own access rule and offered a
        // retry that could only be refused again.
        if (!takeRefusal(err.status))
          error.value = "Не удалось загрузить сообщения";
        return;
      }
      messages.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
      scrollToBottom();
      nextTick(() => initBbcodeInteractive(messagesContainer.value));
      markRead();
    },
  );
}

// The list can arrive after this page has already asked for it, so the load is
// retried the moment the room resolves.
watch(chatRoomId, (id) => {
  if (id && !messages.value.length && !loading.value) loadInitial();
});

// What the server reads to the viewer is its answer to the viewer who asked.
// Signing in from "Войдите, чтобы читать чат игры" does not reload the page,
// so the room is read for the one who has arrived. A sign-out, or a session
// that ran out, reads it again for the guest: an open room stays on screen,
// and a closed one gives way to the invitation to sign in.
useViewerChange(() => loadInitial());

async function loadOlder() {
  if (
    loadingOlder.value ||
    !hasMoreBefore.value ||
    !paging.value?.prevCursor ||
    !chatRoomId.value
  )
    return;
  loadingOlder.value = true;

  const container = messagesContainer.value;
  const heightBefore = container?.scrollHeight ?? 0;

  const { data, error: err } = await gameApi.getChatMessages(
    chatRoomId.value,
    paging.value.prevCursor,
    PAGE_SIZE,
  );
  if (!err && data && data.resources.length) {
    messages.value = [...data.resources, ...messages.value];
    paging.value = {
      ...paging.value,
      prevCursor: data.paging.prevCursor,
      hasPrev: data.paging.hasPrev,
    };
    // Preserve the reader's visual anchor after prepending older content.
    nextTick(() => {
      if (container) {
        container.scrollTop = container.scrollHeight - heightBefore;
      }
      initBbcodeInteractive(messagesContainer.value);
    });
  } else if (!err) {
    paging.value = paging.value ? { ...paging.value, hasPrev: false } : null;
  }
  loadingOlder.value = false;
}

async function markRead() {
  if (!user.value || !chatRoomId.value) return;
  try {
    await gameApi.markChatRoomRead(chatRoomId.value);
  } catch {
    // Non-critical
  }
}

async function handleSend() {
  const text = newMessage.value.trim();
  if (!text || sending.value || !chatRoomId.value) return;
  sending.value = true;
  const { error: err } = await gameApi.sendChatMessage(chatRoomId.value, text);
  sending.value = false;
  if (err) return;
  newMessage.value = "";
  editorRef.value?.clear();
  // Re-fetch the latest window so the new message (and any that arrived
  // meanwhile) render with the server's canonical BBCode + ordering.
  await loadInitial();
}

// ───────────────────────────────────────────────────────────────────────────
// Live tail
// ───────────────────────────────────────────────────────────────────────────
// A chat room has no push of its own, so the newest page is read again on the
// site-wide live-tail cadence while the tab is on screen, and once more the
// moment it returns to the screen. The shape is the one API_DESIGN.md
// ("Realtime") states for every live list: read the tail again and de-duplicate
// by id, and if the tail shares nothing with what is shown, read the room from
// scratch: appending it would leave a hole between the old messages and the
// new ones.

let pollTimer: ReturnType<typeof setInterval> | null = null;
/** A tail read is in flight. See pollForNewer. */
let readingTail = false;

function isAtBottom(): boolean {
  const el = messagesContainer.value;
  if (!el) return true;
  return isNearBottom(el);
}

async function pollForNewer() {
  // A refusal, a missing room and a failed load are not feeds to extend; a
  // load, an older page or a send already owns the window. Nor is the closed
  // room of a guest, which is read to nobody signed out.
  if (document.hidden || !chatRoomId.value || !readsRoom.value) return;
  if (roomRefused.value || denied.value || error.value) return;
  if (loading.value || loadingOlder.value || sending.value) return;
  // Two triggers can land on the same room at once: the timer ticks while the
  // reader brings the tab back, and the return reads too. Both answers would
  // then be deduplicated against the same window, so a message that arrived in
  // between would be appended twice, and a tail with nothing in common would
  // re-read the room twice over.
  if (readingTail) return;

  // The room this answer is being asked for. Everything below is only true of
  // that room, and the reader can leave it while the request is in flight.
  const roomId = chatRoomId.value;
  readingTail = true;
  try {
    const { data, error: err } = await gameApi.getChatMessages(
      roomId,
      undefined,
      PAGE_SIZE,
    );
    // The state the question was asked in may be gone. The reader may have
    // moved to another room, or the window may be being replaced, or the room
    // may have turned out missing or refused meanwhile. A stale tail applied
    // here shows one room's messages under another room's title, and a stale
    // refusal shuts a room the reader is allowed to read.
    if (
      chatRoomId.value !== roomId ||
      loading.value ||
      denied.value ||
      roomRefused.value ||
      error.value
    ) {
      return;
    }

    if (err) {
      // A refusal is the access rule working, exactly as on the initial load,
      // and it is taken as there: the room the reader may no longer read gives
      // way to "not found", and nothing asks again. Dropped unread it was
      // asked again on the next tick and on every tick after it, and the
      // response interceptor put its toast on screen once per interval.
      takeRefusal(err.status);
      // Anything else is silent: a failed background read must not replace
      // what the reader is reading, and the next tick asks again.
      return;
    }
    if (!data) return;

    const arrived = data.resources ?? [];
    if (!arrived.length) return;
    const shown = new Set(messages.value.map((m) => m.id));
    const fresh = arrived.filter((m) => !shown.has(m.id));
    if (!fresh.length) return;

    const wasAtBottom = isAtBottom();
    if (messages.value.length && fresh.length === arrived.length) {
      // Not one line of the newest page is on screen: more went by than a page
      // holds, so appending it would leave a hole between what is shown and
      // what arrived, with nothing on screen to say a stretch of the room is
      // missing. The page in hand IS the window to show, applied here with its
      // own paging instead of being asked for a second time. Reading the room
      // again through loadInitial raised `loading`, so the feed the reader was
      // reading blinked into skeletons, and it took him to the end of a
      // conversation he had not asked to be taken to.
      //
      // Which is the other half of it: the window under a reader who has
      // scrolled up into the history IS the page he is reading, and replacing
      // it takes that page away wherever the replacement comes from. So it
      // waits for his return to the tail, and the tick that finds him there
      // makes it.
      if (!wasAtBottom) return;
      messages.value = arrived;
      paging.value = data.paging ?? null;
    } else {
      messages.value = [...messages.value, ...fresh];
      // Paging is deliberately left alone: prevCursor points before the OLDEST
      // message held, and the latest page knows nothing about the older ones
      // this reader has already scrolled in.
    }
    nextTick(() => initBbcodeInteractive(messagesContainer.value));
    if (!wasAtBottom) return;
    scrollToBottom();
    // What the page shows is read, taken literally: the mark moves only for a
    // reader the new messages have actually reached. A reader parked higher up
    // has not seen them, and clearing his unread mark would take away the one
    // sign that they arrived.
    markRead();
  } finally {
    readingTail = false;
  }
}

function startPolling() {
  if (pollTimer) return;
  pollTimer = setInterval(pollForNewer, LIVE_TAIL_POLL_INTERVAL_MS);
}

function stopPolling() {
  if (pollTimer) {
    clearInterval(pollTimer);
    pollTimer = null;
  }
}

// A hidden tab reads nothing: ticks would pile requests on a room nobody is
// looking at, and the return to the screen catches up in one read anyway.
function handleVisibilityChange() {
  if (document.hidden) {
    stopPolling();
    return;
  }
  startPolling();
  pollForNewer();
}

// Top sentinel for infinite older-message loading.
const topSentinel = ref<HTMLElement | null>(null);
let topObserver: IntersectionObserver | null = null;

function setupObserver() {
  cleanupObserver();
  if (!messagesContainer.value || !topSentinel.value) return;
  topObserver = new IntersectionObserver(
    (entries) => {
      if (entries[0].isIntersecting) loadOlder();
    },
    { root: messagesContainer.value, rootMargin: "100px 0px 0px 0px" },
  );
  topObserver.observe(topSentinel.value);
}
function cleanupObserver() {
  topObserver?.disconnect();
  topObserver = null;
}

watch([hasMoreBefore, messagesContainer], () => {
  nextTick(setupObserver);
});

watch([gameId, roomNum], loadInitial, { immediate: true });

onMounted(() => {
  nextTick(setupObserver);
  if (!document.hidden) startPolling();
  document.addEventListener("visibilitychange", handleVisibilityChange);
});
onUnmounted(() => {
  cleanupObserver();
  stopPolling();
  document.removeEventListener("visibilitychange", handleVisibilityChange);
});
</script>

<template>
  <div class="chat-room" :class="{ 'with-feed': showsFeed }">
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

    <!-- A guest reads an open room and no closed one. A closed room the list
         closes to him has no page here, and gives way to "not found"
         (roomRefused); in place of one the server would not read to him,
         the one thing he can do about it. -->
    <LoginPrompt v-if="invitesSignIn" action="читать чат игры" />

    <template v-else-if="showsFeed">
      <div
        ref="messagesContainer"
        class="chat-messages"
        :class="{ 'layout-compact': isCompactLayout }"
        role="log"
        aria-label="Сообщения чата"
      >
        <!-- Also while the room is still to resolve out of the list: until
             then it may yet turn out to be one with no page here. The rows
             it stands for are messages of a chat, so the full look waits in
             the skeleton of the site's chat (ChatFeed), avatar 72 and all. -->
        <template v-if="loading || (!roomOpen && !error)">
          <CommentSkeleton v-if="isCompactLayout" :count="5" />
          <ChatMessageSkeleton v-else class="chat-skeleton" :count="5" />
        </template>

        <div v-else-if="error" class="chat-error" role="alert">
          <secondary-text>{{ error }}</secondary-text>
          <button type="button" class="chat-retry" @click="loadInitial">
            Повторить
          </button>
        </div>

        <secondary-text v-else-if="!messages.length" class="chat-empty-inline">
          Сообщений пока нет
        </secondary-text>

        <template v-else>
          <div
            v-if="hasMoreBefore"
            ref="topSentinel"
            class="top-sentinel"
          ></div>

          <template
            v-for="item in messagesWithSeparators"
            :key="isDateSeparator(item) ? `sep-${item.date}` : item.id"
          >
            <div v-if="isDateSeparator(item)" class="date-separator">
              <div class="separator-line"></div>
              <span class="separator-text">{{ item.formattedDate }}</span>
              <div class="separator-line"></div>
            </div>
            <div
              v-else
              :id="`msg-${item.id}`"
              class="chat-message-row"
              :class="{ continuation: item.isContinuation }"
            >
              <ChatMessage
                :message="item"
                :compact="isCompactLayout"
                :is-online="isOnline(item.author)"
                :max-height="MAX_MESSAGE_HEIGHT"
              />
            </div>
          </template>
        </template>
      </div>

      <!-- Send form -->
      <div class="chat-input-wrapper">
        <template v-if="canSend">
          <BBCodeEditor
            ref="editorRef"
            v-model="newMessage"
            context="message"
            placeholder="Написать сообщение..."
            :draft-key="composerDraftKey('room', 'message', chatRoomId)"
            :disabled="sending"
            :min-height="60"
            :max-height="200"
            @submit="handleSend"
          />
          <Button
            type="button"
            variant="primary"
            class="chat-send-button"
            :disabled="sending || !newMessage.trim()"
            @click="handleSend"
          >
            Отправить
          </Button>
        </template>
        <RefusalNote v-else-if="showsWhoWrites" class="chat-write-hint">
          Писать здесь могут мастер, ассистенты и те, кому мастер разрешил
          писать в этой комнате
        </RefusalNote>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *
@use "@/assets/styles/Inputs" as *

// The height of a feed, held only where there is one. A room a guest is
// invited to sign in to is one plaque, and the height of a feed under it would
// be an empty gap down to the next block.
.chat-room.with-feed
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

.chat-messages
  height: calc(100vh - 360px)
  min-height: $grid-step * 40
  overflow-y: auto
  overflow-x: hidden
  border: 1px dashed $border
  padding: 0 $small
  &::after
    content: ""
    display: block
    height: $medium

.top-sentinel
  width: 100%
  height: 1px

.date-separator
  display: flex
  align-items: center
  gap: $small
  padding: $small
  margin: $small 0

.separator-line
  flex: 1
  height: 0
  border-top: 1px dashed $border

.separator-text
  flex-shrink: 0
  padding: 0 $small
  font-size: $secondary-font-size
  color: $text-muted
  font-weight: 500
  white-space: nowrap

// The skeleton's rows where the rows below stand: this frame holds $small at
// the sides and each row its own $small, where the frame of the site's chat
// holds nothing and its rows $medium.
.chat-skeleton :deep(.skeleton-message)
  padding: $small

.chat-message-row
  padding: $small
  margin-bottom: $medium
  word-break: break-word
  overflow-wrap: break-word
  background-color: $bg-page
  &.continuation
    margin-bottom: $tiny
    margin-top: -$small

.chat-error
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  padding: $big

.chat-empty-inline
  display: block
  padding: $big

.chat-retry
  +button

.chat-input-wrapper
  margin-top: $medium
  display: flex
  flex-direction: column
  gap: $small

  :deep(.bbcode-editor-wrapper)
    width: 100%

.chat-send-button
  align-self: flex-start

// Compact display — the compact header/content layout is handled by
// ChatMessage's `compact` prop; only the row spacing is tuned here.
.chat-messages.layout-compact
  .chat-message-row
    padding: $tiny $small
    margin-bottom: $small
    &.continuation
      margin-top: 0
</style>
