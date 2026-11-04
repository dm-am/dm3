<script setup lang="ts">
// A room of the chat type: messages paged by cursor, keyed by the room's id.
import { computed, ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import type { Message } from "@/shared/api/models/common";
import {
  groupMessagesWithSeparators,
  isDateSeparator,
  isUserOnline,
  latestActivityByUsername,
  type MessageOrSeparator,
} from "@/shared/lib/utils/chat";
import { ChatMessage } from "@/widgets/chat-message";
import { LoginPrompt } from "@/features/auth";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { ErrorState } from "@/shared/ui/ErrorState";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import RefusalNote from "@/shared/ui/Layout/RefusalNote.vue";
import { BackLink } from "@/shared/ui/Layout";
import { ChatMessageSkeleton, CommentSkeleton } from "@/shared/ui/Skeleton";
import {
  useZoneMarker,
  useZoneSection,
} from "@/shared/lib/composables/useZoneSection";
import Button from "@/shared/ui/Button/Button.vue";
import { useChatRoomAdmission } from "./room/useChatRoomAdmission";
import { useRoomNotFound } from "./room/useRoomNotFound";
import { useChatRoomMessages } from "./room/useChatRoomMessages";
import { useChatRoomLiveTail } from "./room/useChatRoomLiveTail";

const MAX_MESSAGE_HEIGHT = 300;

const route = useRoute();
const { game, rooms } = storeToRefs(useGameDetailsStore());
const { user } = storeToRefs(useAuthStore());
const { isCompactLayout } = storeToRefs(useUiStore());

const gameId = computed(() => route.params.id as string);
const roomNum = computed(() => parseInt(route.params.num as string, 10));

const room = computed(
  () => rooms.value.find((r) => r.roomNumber === roomNum.value) ?? null,
);
const chatRoomId = computed(() => room.value?.id ?? null);

const admission = useChatRoomAdmission(room);
const { roomOpen, mayWrite, invitesSignIn, denied } = admission;
const { ensureRooms, roomRefused } = useRoomNotFound({
  gameId,
  roomNum,
  refusedByServer: admission.serverRefused,
});

// The room's name is data, so it cannot sit in meta.section.
useZoneSection(() => room.value?.title);
useZoneMarker(() => (room.value?.isArchived ? "архив" : undefined));

const newMessage = ref("");
const messagesContainer = ref<HTMLElement | null>(null);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

const feed = useChatRoomMessages({
  gameId,
  roomNum,
  chatRoomId,
  container: messagesContainer,
  admission,
  ensureRooms,
});
const {
  messages,
  loading,
  sending,
  error,
  hasMoreBefore,
  topSentinel,
  loadInitial,
} = feed;
useChatRoomLiveTail({
  chatRoomId,
  container: messagesContainer,
  admission,
  roomRefused,
  feed,
});

const messagesWithSeparators = computed((): MessageOrSeparator[] =>
  groupMessagesWithSeparators(messages.value),
);

const canSend = computed(
  () => !!user.value && !denied.value && roomOpen.value && mayWrite.value,
);

const showsFeed = computed(() => !invitesSignIn.value && !roomRefused.value);

// Not before the roles arrive, so a master never sees it blink; a guest gets it
// too, since signing in lets nobody write here by itself.
const showsWhoWrites = computed(
  () =>
    showsFeed.value &&
    !error.value &&
    !loading.value &&
    roomOpen.value &&
    !!game.value &&
    !mayWrite.value,
);

const latestActivity = computed(() => latestActivityByUsername(messages.value));
function isOnline(author: Message["author"]): boolean {
  if (!author?.username) return false;
  return isUserOnline(latestActivity.value.get(author.username));
}

async function handleSend() {
  if (!(await feed.send(newMessage.value))) return;
  newMessage.value = "";
  editorRef.value?.clear();
  // The server's rendering and order, with whatever arrived meanwhile.
  await loadInitial();
}
</script>

<template>
  <div class="chat-room" :class="{ 'with-feed': showsFeed }">
    <BackLink
      :to="{ name: 'game', params: { id: gameId } }"
      label="Назад к игре"
    />

    <LoginPrompt v-if="invitesSignIn" action="читать чат игры" />

    <template v-else-if="showsFeed">
      <div
        ref="messagesContainer"
        class="chat-messages"
        :class="{ 'layout-compact': isCompactLayout }"
        role="log"
        aria-label="Сообщения чата"
      >
        <!-- Also while the room is unresolved: it may have no page here. -->
        <template v-if="loading || (!roomOpen && !error)">
          <CommentSkeleton v-if="isCompactLayout" :count="5" />
          <ChatMessageSkeleton v-else class="chat-skeleton" :count="5" />
        </template>

        <ErrorState
          v-else-if="error"
          class="chat-error"
          :message="error"
          :retry="loadInitial"
        />

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
            <DashSeparator
              v-if="isDateSeparator(item)"
              :label="item.formattedDate"
            />
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
// Only with a feed: under the sign-in plaque it would be an empty gap.
.chat-room.with-feed
  min-height: $grid-step * 50

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

// This frame and its rows hold $small each, unlike the frame of the site chat.
.chat-skeleton :deep(.skeleton-message)
  padding: $small

.chat-message-row
  padding: $small
  margin-bottom: $medium
  overflow-wrap: anywhere
  background-color: $bg-page
  &.continuation
    margin-bottom: $tiny
    margin-top: -$small

.chat-error
  margin: $small 0

.chat-empty-inline
  padding: $big

.chat-input-wrapper
  margin-top: $medium
  display: flex
  flex-direction: column
  gap: $small

  :deep(.bbcode-editor-wrapper)
    width: 100%

.chat-send-button
  align-self: flex-start

.chat-messages.layout-compact
  .chat-message-row
    padding: $tiny $small
    margin-bottom: $small
    &.continuation
      margin-top: 0
</style>
