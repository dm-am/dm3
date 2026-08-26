<script setup lang="ts">
// Chat-type game room (RoomType.Chat): a message-based, cursor-paginated
// out-of-character room. Resolves the Room by its number from the store, then
// drives the message stream off the chat gameApi keyed by that room's id
// (the chat-rooms/{id}/messages endpoints resolve the room's linked chatId
// server-side). Rendering reuses the shared ChatMessage widget; sending
// reuses the BBCodeEditor.
import { computed, ref, watch, nextTick, onMounted, onUnmounted } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import { gameApi } from "@/entities/game";
import type { Message, CursorPaging } from "@/shared/api/models/common";
import {
  groupMessagesWithSeparators,
  isDateSeparator,
  isUserOnline,
  type MessageOrSeparator,
} from "@/shared/lib/utils/chat";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";
import { ChatMessage } from "@/widgets/chat-message";
import { LoginPrompt } from "@/features/auth";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { CommentSkeleton } from "@/shared/ui/Skeleton";
import { useZoneSection } from "@/shared/lib/composables/useZoneSection";

const PAGE_SIZE = 50;
const MAX_MESSAGE_HEIGHT = 300;

const route = useRoute();
const gameStore = useGameDetailsStore();
const { rooms, roomsLoading } = storeToRefs(gameStore);
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

// The room IS the section here, and its name is data — meta.section cannot
// spell it. Announced to the shell, which is the only place that writes a
// heading or a tab name in this zone.
useZoneSection(() => room.value?.title);

// ───────────────────────────────────────────────────────────────────────────
// Message stream (cursor pagination)
// ───────────────────────────────────────────────────────────────────────────
const messages = ref<Message[]>([]);
const paging = ref<CursorPaging | null>(null);
const loading = ref(false);
const loadingOlder = ref(false);
const sending = ref(false);
const error = ref<string | null>(null);
const notFound = ref(false);
/** The server refused the room: the viewer is not playing this game. */
const denied = ref(false);

const hasMoreBefore = computed(() => paging.value?.hasPrev ?? false);

const messagesWithSeparators = computed((): MessageOrSeparator[] =>
  groupMessagesWithSeparators(messages.value),
);

const newMessage = ref("");
const messagesContainer = ref<HTMLElement | null>(null);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

// Sending needs the same admission reading does: a viewer the server turned
// away gets the refusal above, not an editor that fails on submit.
const canSend = computed(() => !!user.value && !denied.value);

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

async function ensureRooms() {
  if (!rooms.value.length) await gameStore.loadRooms(gameId.value);
}

async function loadInitial() {
  loading.value = true;
  error.value = null;
  notFound.value = false;
  messages.value = [];
  paging.value = null;

  await ensureRooms();
  // Not found means the list is in and the room is not in it. That used to be
  // a guess: the shell loads the same list, and two requests for it left one
  // answer discarded, so a page that awaited the losing one saw an empty list
  // and said the room does not exist while it was on the screen behind the
  // message. loadRooms now hands both callers the same request, so the await
  // above really does mean the list is in. The roomsLoading branch below stays
  // as the floor for the case this page cannot see — a reset between the two,
  // which drops the answer on purpose.
  if (!chatRoomId.value) {
    if (roomsLoading.value) {
      loading.value = false;
      return;
    }
    notFound.value = true;
    loading.value = false;
    return;
  }

  const { data, error: err } = await gameApi.getChatMessages(
    chatRoomId.value,
    undefined,
    PAGE_SIZE,
  );
  loading.value = false;
  if (err) {
    // A chat room is for the people playing: the server admits participants and
    // leads only (spec 4.2.3.5.3), so a refusal here is the rule working, not a
    // failure. Saying "не удалось загрузить" to a guest blamed the site for its
    // own access rule and invited a pointless retry.
    denied.value = err.status === 401 || err.status === 403;
    error.value = denied.value ? "" : "Не удалось загрузить сообщения";
    return;
  }
  messages.value = data?.resources ?? [];
  paging.value = data?.paging ?? null;
  scrollToBottom();
  nextTick(() => initBbcodeInteractive(messagesContainer.value));
  markRead();
}

// The list can arrive after this page has already asked for it, so the load is
// retried the moment the room resolves.
watch(chatRoomId, (id) => {
  if (id && !messages.value.length && !loading.value) loadInitial();
});

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
});
onUnmounted(cleanupObserver);
</script>

<template>
  <div class="chat-room">
    <!-- Back link -->
    <router-link
      :to="{ name: 'game', params: { id: gameId } }"
      class="back-link"
    >
      <SvgIcon name="chevronLeft" />
      Назад к игре
    </router-link>

    <block-title v-if="room"
      >Чат: {{ room.title
      }}<template v-if="room.isArchived"
        >{{ " " }}<span class="archived-tag">архив</span></template
      ></block-title
    >

    <div v-if="notFound" class="chat-empty">
      <secondary-text>Комната №{{ roomNum }} не найдена</secondary-text>
    </div>

    <template v-else>
      <div
        ref="messagesContainer"
        class="chat-messages"
        :class="{ 'layout-compact': isCompactLayout }"
        role="log"
        aria-label="Сообщения чата"
      >
        <CommentSkeleton v-if="loading" :count="5" />

        <div v-else-if="denied" class="chat-empty-inline">
          <secondary-text>
            Чат комнаты доступен участникам игры: игрокам с активным персонажем,
            мастеру, ассистентам и наставнику.
          </secondary-text>
          <LoginPrompt v-if="!user" action="читать чат игры" />
        </div>

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
          <button
            type="button"
            class="chat-send-button"
            :disabled="sending || !newMessage.trim()"
            @click="handleSend"
          >
            Отправить
          </button>
        </template>
        <LoginPrompt v-else-if="!denied" action="отправлять сообщения" />
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *
@use "@/assets/styles/Inputs" as *

.chat-room
  min-height: $grid-step * 50

// Muted "архив" tag beside an archived room's title — archive is conveyed by
// this marker + muted colour, never by a suffix inside the room name. The gap
// in front of it is the " " text node in the title, not a margin: a margin is
// drawn but not copied, and the title used to reach the clipboard glued.
.archived-tag
  font-size: $secondary-font-size
  font-weight: normal
  color: $text-muted
  text-transform: uppercase
  vertical-align: middle

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

.chat-message-row
  padding: $small
  margin-bottom: $medium
  word-break: break-word
  overflow-wrap: break-word
  background-color: $bg-page
  &.continuation
    margin-bottom: $tiny
    margin-top: -$small

.chat-empty,
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
  +button

// Compact display — the compact header/content layout is handled by
// ChatMessage's `compact` prop; only the row spacing is tuned here.
.chat-messages.layout-compact
  .chat-message-row
    padding: $tiny $small
    margin-bottom: $small
    &.continuation
      margin-top: 0
</style>
