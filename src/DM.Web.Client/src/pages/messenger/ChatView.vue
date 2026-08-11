<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useMessagingStore } from "@/entities/message";
import {
  useAuthStore,
  useMessagePermissions,
  AvatarImg,
} from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import {
  groupMessagesWithSeparators,
  isDateSeparator,
  isUserOnline,
  type MessageOrSeparator,
} from "@/shared/lib/utils/chat";
import {
  joinTitleSegments,
  useDocumentTitle,
  useVirtualScroll,
} from "@/shared/lib/composables";
import { ChatMessage } from "@/widgets/chat-message";
import { Tooltip } from "@/shared/ui/Tooltip";
import type { ChatId, Message, MessageId } from "@/entities/message";
import dayjs from "dayjs";
import { symbols } from "@/shared/lib/utils/icons";
import { SvgIcon } from "@/shared/ui/Icon";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { messagingApi } from "@/entities/message";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";
import { notifyFailure } from "@/shared/lib/errors";
import { useMessageToolbar } from "@/shared/lib/composables/useMessageToolbar";
import {
  useAnchoredInfiniteScroll,
  LANDING_SCROLL_MS,
} from "@/shared/lib/composables/useAnchoredInfiniteScroll";

const route = useRoute();
const router = useRouter();
const messagingStore = useMessagingStore();
const { user: currentUser } = storeToRefs(useAuthStore());
const { isCompactLayout } = storeToRefs(useUiStore());
const {
  selectedChat,
  messagesList,
  sending,
  interlocutor,
  hasMoreBefore,
  hasMoreAfter,
  errorBefore,
} = storeToRefs(messagingStore);

// The interlocutor first: three messenger routes shared one tab name, and the
// name of the person is the only thing that tells two correspondences apart.
useDocumentTitle(() =>
  joinTitleSegments(interlocutor.value?.username, "Личные сообщения"),
);

const MAX_MESSAGE_HEIGHT = 200;

// Message permissions (shared composable)
const {
  isModerator,
  canEdit: canEditMsg,
  canDelete: canDeleteMsg,
  canLike: canLikeMsg,
} = useMessagePermissions(currentUser);

// Private correspondence is deliberately outside the ordinary ban, so the only
// condition here is being signed in. The server agrees: the ban check in
// ChatIntentionResolver applies to the global chat branch alone.
const canSendMessages = computed(() => !!currentUser.value);

const newMessage = ref("");
const messagesContainer = ref<HTMLElement | null>(null);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const topSentinel = ref<HTMLElement | null>(null);

// Scroll position tracking
let isScrolling = false;
let scrollEndTimeout: ReturnType<typeof setTimeout> | null = null;

// Infinite scroll: shared with the global chat. Only the older direction
// exists here — this list ends at the newest message, so there is nothing
// below to page into.
const {
  loadOlder,
  setupInfiniteScroll,
  suspend: suspendInfiniteScroll,
  resume: resumeInfiniteScroll,
} = useAnchoredInfiniteScroll({
  container: messagesContainer,
  older: {
    sentinel: topSentinel,
    hasMore: () => hasMoreBefore.value,
    load: () => messagingStore.fetchMoreBefore(),
    failed: () => Boolean(errorBefore.value),
  },
});

// Group messages with date separators (shared utility)
const messagesWithSeparators = computed((): MessageOrSeparator[] =>
  groupMessagesWithSeparators(messagesList.value ?? []),
);
// isDateSeparator imported from shared/lib/utils/chat

// Virtual scroll
const itemCount = computed(() => messagesWithSeparators.value.length);
const { virtualItems, totalSize, measureElement, scrollToIndex } =
  useVirtualScroll({
    count: itemCount,
    container: messagesContainer,
    estimateSize: 80,
    overscan: 15,
  });

// Edit state
const editingId = ref<string | null>(null);
const editText = ref("");

// Delete confirmation state
const toolbarEl = ref<HTMLElement | null>(null);

// Hover toolbar state
// Shared with the global chat. The keyboard path below arrives with it: this
// view had hover only, so a keyboard user could reach every message action in
// one chat and none in the other.
const {
  hoveredMessageId,
  toolbarPosition,
  isToolbarHovered,
  confirmingDeleteId,
  handleMessageMouseEnter,
  handleMessageFocusIn,
  handleMessageMouseLeave,
  handleMessageFocusOut,
  handleToolbarMouseEnter,
  handleToolbarMouseLeave,
  handleToolbarFocusIn,
  handleToolbarFocusOut,
  handleScrollStart,
} = useMessageToolbar({
  toolbar: toolbarEl,
  isScrolling: () => isScrolling,
});

const hoveredMessage = computed(() => {
  if (!hoveredMessageId.value) return null;
  return (
    messagesList.value.find((m) => m.id === hoveredMessageId.value) || null
  );
});

// Expanded messages
const expandedDeletedMessages = ref<Set<string>>(new Set());

// Scroll the virtualized list to a message and flash it (jump-to-context).
function scrollToMessage(msgId: string) {
  const index = messagesWithSeparators.value.findIndex(
    (item) => !isDateSeparator(item) && (item as Message).id === msgId,
  );
  if (index >= 0) {
    scrollToIndex(index, { align: "center" });
  }
  nextTick(() => {
    setTimeout(() => {
      const element = document.getElementById(`msg-${msgId}`);
      if (element) {
        element.classList.add("highlighted");
        setTimeout(() => {
          element.classList.remove("highlighted");
          messagingStore.clearHighlight();
        }, 1500);
      }
    }, 100);
  });
}

async function loadChat() {
  const id = route.params.id as ChatId;
  await messagingStore.selectChat(id);
  if (selectedChat.value) {
    // ?msg=<id> (from search jump-to-context) loads the window around a
    // specific message instead of the latest page.
    const jumpMsgId =
      typeof route.query.msg === "string" ? route.query.msg : null;
    if (jumpMsgId) {
      // Landing in the middle of the history sweeps the list past the top
      // sentinel; the observers stay off until that scroll has settled, or the
      // landing itself pages in history the reader never asked for.
      suspendInfiniteScroll();
      await messagingStore.navigateToMessage(id, jumpMsgId as MessageId);
      await messagingStore.markAsRead(id);
      nextTick(() => {
        setupInfiniteScroll();
        scrollToMessage(jumpMsgId);
        setTimeout(resumeInfiniteScroll, LANDING_SCROLL_MS);
      });
      return;
    }
    await messagingStore.fetchMessages(id);
    await messagingStore.markAsRead(id);
    scrollToBottom();
    nextTick(() => {
      setupInfiniteScroll();
    });
  }
}

function scrollToBottom() {
  nextTick(() => {
    if (messagesContainer.value) {
      messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight;
    }
  });
}

function handleWheel() {
  isScrolling = true;
  handleScrollStart();
  if (scrollEndTimeout) {
    clearTimeout(scrollEndTimeout);
  }
  scrollEndTimeout = setTimeout(() => {
    isScrolling = false;
    scrollEndTimeout = null;
  }, 150);
}

function handleScroll() {
  isScrolling = true;
  if (hoveredMessageId.value) {
    hoveredMessageId.value = null;
    confirmingDeleteId.value = null;
    isToolbarHovered.value = false;
  }
  if (scrollEndTimeout) {
    clearTimeout(scrollEndTimeout);
  }
  scrollEndTimeout = setTimeout(() => {
    isScrolling = false;
    scrollEndTimeout = null;
  }, 150);
}

// Header formatting (time / tooltips) is fully owned by ChatMessage — the
// page renders no header markup of its own.

// Track latest activity per username
const latestActivityByUsername = computed(() => {
  const map = new Map<string, string>();
  if (!messagesList.value?.length) return map;
  for (const msg of messagesList.value) {
    if (!msg.author?.username || !msg.author?.lastActivityUtc) continue;
    const existing = map.get(msg.author.username);
    if (
      !existing ||
      dayjs(msg.author.lastActivityUtc).isAfter(dayjs(existing))
    ) {
      map.set(msg.author.username, msg.author.lastActivityUtc);
    }
  }
  return map;
});

function isOnline(author: any) {
  if (!author?.username) return false;
  const lastActivityUtc = latestActivityByUsername.value.get(author.username);
  return isUserOnline(lastActivityUtc);
}

// Permissions — delegated to shared composable
function canEditMessage(msg: Message) {
  return canEditMsg(msg);
}
function canDeleteMessage(msg: Message) {
  return canDeleteMsg(msg);
}
function canLikeMessage(msg: Message) {
  return canLikeMsg(msg);
}

function isLikedByMe(msg: Message) {
  if (!currentUser.value) return false;
  return (
    msg.likes?.some((u: any) => u.username === currentUser.value?.username) ??
    false
  );
}

// Edit
function isEditing(msgId: string) {
  return editingId.value === msgId;
}

async function startEdit(msg: Message) {
  editingId.value = msg.id;
  // Fetch the original BBCode from the backend — seeds ChatMessage's editor
  // once; further keystrokes stay inside ChatMessage's own local state.
  const { data } = await messagingApi.getMessageForEdit(msg.id);
  editText.value = data?.text || "";
}

function cancelEdit() {
  editingId.value = null;
  editText.value = "";
}

// Receives the edited text straight from ChatMessage's @save-edit payload
// (its own local editor state) — same contract as GlobalChatPage; the page
// must not read back its own stale editText seed.
async function saveEditWithText(msgId: string, text: string) {
  if (text.trim()) {
    const { error } = await messagingStore.updateMessage(msgId, text);
    // The editor stays open with the text still in it: a closed editor over
    // the unchanged message says the edit went through.
    if (error) {
      notifyFailure(error, "Не удалось сохранить сообщение");
      return;
    }
  }
  cancelEdit();
}

// Message truncation (long messages, [cut] markers, expand state) is now
// fully owned by ChatMessage via <TruncatedContent>. The page only forwards
// MAX_MESSAGE_HEIGHT to ChatMessage as the per-message collapse budget.

// Deleted messages expand
function isDeletedExpanded(msgId: string) {
  return expandedDeletedMessages.value.has(msgId);
}

function toggleDeletedExpand(msgId: string) {
  if (!isModerator.value) return;
  if (expandedDeletedMessages.value.has(msgId)) {
    expandedDeletedMessages.value.delete(msgId);
  } else {
    expandedDeletedMessages.value.add(msgId);
  }
}

// Likes
async function toggleLike(msg: Message) {
  if (!currentUser.value) return;
  if (isLikedByMe(msg)) {
    await messagingStore.unlikeMessage(msg.id);
  } else {
    await messagingStore.likeMessage(msg.id);
  }
}

// Jump to latest
async function jumpToLatest() {
  await messagingStore.jumpToLatest();
  scrollToBottom();
}

async function handleSend() {
  if (!newMessage.value.trim() || sending.value || !selectedChat.value) return;
  const text = newMessage.value;
  newMessage.value = "";
  const { error } = await messagingStore.sendMessage(
    selectedChat.value.id,
    text,
  );
  // Give the text back on failure. Emptying the field before the request is
  // what makes sending feel instant; losing what was written when it fails is
  // not part of that bargain. The editor's own clear() waits for the send to
  // land — it also drops the saved draft, and that copy is the one that
  // outlives the tab.
  if (error) {
    newMessage.value = text;
    notifyFailure(error, "Не удалось отправить сообщение");
    return;
  }
  editorRef.value?.clear();
  scrollToBottom();
}

function requestDelete(id: string) {
  confirmingDeleteId.value = id;
}

function cancelDelete() {
  confirmingDeleteId.value = null;
}

async function confirmDelete() {
  if (!confirmingDeleteId.value) return;
  const { error } = await messagingStore.deleteMessage(
    confirmingDeleteId.value,
  );
  confirmingDeleteId.value = null;
  if (error) notifyFailure(error, "Не удалось удалить сообщение");
}

function goBack() {
  router.push({ name: "messenger" });
}

// React to chat id changes and to jumping between messages within the same
// chat (?msg changes but params.id does not).
watch(() => [route.params.id, route.query.msg], loadChat, { immediate: true });

// New messages bring new BBCode into the feed. Only the LENGTH is watched, the
// way the global chat next door already does it: a deep watch walked every
// message object on every nested mutation — a like, a read flag — and then
// re-scanned the whole container, while each ChatMessage already initialises
// its own markup through TruncatedContent's on-content-mounted. In a chat with
// five hundred loaded messages that ran on every incoming push.
watch(
  () => messagesList.value?.length,
  () => {
    nextTick(() => {
      initBbcodeInteractive(messagesContainer.value);
    });
  },
);

onMounted(() => {
  messagesContainer.value?.addEventListener("wheel", handleWheel, {
    passive: true,
  });
  messagesContainer.value?.addEventListener("scroll", handleScroll);
  nextTick(() => {
    initBbcodeInteractive(messagesContainer.value);
  });
});

onUnmounted(() => {
  messagingStore.clearSelection();
  messagesContainer.value?.removeEventListener("wheel", handleWheel);
  messagesContainer.value?.removeEventListener("scroll", handleScroll);
  if (scrollEndTimeout) clearTimeout(scrollEndTimeout);
});
</script>

<template>
  <div class="chat-view">
    <template v-if="selectedChat">
      <page-title v-if="interlocutor"
        >Переписка с {{ interlocutor.username }}</page-title
      >
      <page-title v-else>Переписка</page-title>

      <div class="chat-header">
        <button class="back-button" @click="goBack">
          {{ symbols.arrowLeft }} Назад
        </button>
        <router-link
          v-if="interlocutor"
          :to="{ name: 'profile', params: { username: interlocutor.username } }"
          class="interlocutor"
        >
          <AvatarImg
            :picture="interlocutor.picture"
            :alt="interlocutor.username"
            :size="40"
            img-class="header-avatar"
            eager
          />
          <span class="username">{{ interlocutor.username }}</span>
        </router-link>
        <span v-else class="username">Загрузка...</span>
      </div>

      <div
        class="messages-wrapper"
        :class="{ 'layout-compact': isCompactLayout }"
      >
        <div ref="messagesContainer" class="messages-container">
          <template v-if="!messagesList?.length">
            <secondary-text class="empty-messages">
              Начните переписку, отправив первое сообщение
            </secondary-text>
          </template>

          <template v-else>
            <!-- Top sentinel for loading older messages -->
            <div
              v-if="hasMoreBefore"
              ref="topSentinel"
              class="scroll-sentinel top-sentinel"
            >
              <secondary-text v-if="errorBefore" class="sentinel-error">
                {{ errorBefore }}
                <button
                  type="button"
                  class="sentinel-retry"
                  @click="loadOlder()"
                >
                  Повторить
                </button>
              </secondary-text>
            </div>

            <!-- Virtual scroll container -->
            <div
              :style="{
                height: `${totalSize}px`,
                width: '100%',
                position: 'relative',
              }"
            >
              <div
                v-for="virtualRow in virtualItems"
                :key="String(virtualRow.key)"
                :ref="
                  (el) => {
                    if (el) measureElement(el as HTMLElement);
                  }
                "
                :data-index="virtualRow.index"
                :style="{
                  position: 'absolute',
                  top: 0,
                  left: 0,
                  width: '100%',
                  transform: `translateY(${virtualRow.start}px)`,
                }"
              >
                <template
                  v-if="
                    isDateSeparator(messagesWithSeparators[virtualRow.index])
                  "
                >
                  <div class="date-separator">
                    <div class="separator-line"></div>
                    <span class="separator-text">{{
                      (messagesWithSeparators[virtualRow.index] as any)
                        .formattedDate
                    }}</span>
                    <div class="separator-line"></div>
                  </div>
                </template>
                <template v-else>
                  <div
                    :id="`msg-${(messagesWithSeparators[virtualRow.index] as any).id}`"
                    class="pm-message"
                    tabindex="0"
                    :class="{
                      removed: (messagesWithSeparators[virtualRow.index] as any)
                        .isRemoved,
                      hovered:
                        hoveredMessageId ===
                        (messagesWithSeparators[virtualRow.index] as any).id,
                      continuation: (
                        messagesWithSeparators[virtualRow.index] as any
                      ).isContinuation,
                    }"
                    @mouseenter="
                      handleMessageMouseEnter(
                        $event,
                        (messagesWithSeparators[virtualRow.index] as any).id,
                      )
                    "
                    @mouseleave="handleMessageMouseLeave"
                    @focusin="
                      handleMessageFocusIn(
                        $event,
                        (messagesWithSeparators[virtualRow.index] as any).id,
                      )
                    "
                    @focusout="handleMessageFocusOut"
                  >
                    <ChatMessage
                      :message="messagesWithSeparators[virtualRow.index] as any"
                      :compact="isCompactLayout"
                      :hovered="
                        hoveredMessageId ===
                        (messagesWithSeparators[virtualRow.index] as any).id
                      "
                      :is-online="
                        isOnline(
                          (messagesWithSeparators[virtualRow.index] as any)
                            .author,
                        )
                      "
                      :is-liked-by-me="
                        isLikedByMe(
                          messagesWithSeparators[virtualRow.index] as any,
                        )
                      "
                      :can-edit="
                        canEditMessage(
                          messagesWithSeparators[virtualRow.index] as any,
                        )
                      "
                      :can-delete="
                        canDeleteMessage(
                          messagesWithSeparators[virtualRow.index] as any,
                        )
                      "
                      :can-like="
                        canLikeMessage(
                          messagesWithSeparators[virtualRow.index] as any,
                        )
                      "
                      :is-moderator="isModerator"
                      :is-editing="
                        isEditing(
                          (messagesWithSeparators[virtualRow.index] as any).id,
                        )
                      "
                      :edit-text="editText"
                      :is-deleted-expanded="
                        isDeletedExpanded(
                          (messagesWithSeparators[virtualRow.index] as any).id,
                        )
                      "
                      :max-height="MAX_MESSAGE_HEIGHT"
                      @like="
                        toggleLike(
                          messagesWithSeparators[virtualRow.index] as any,
                        )
                      "
                      @toggle-deleted="
                        toggleDeletedExpand(
                          (messagesWithSeparators[virtualRow.index] as any).id,
                        )
                      "
                      @start-edit="
                        startEdit(
                          messagesWithSeparators[virtualRow.index] as any,
                        )
                      "
                      @save-edit="
                        (text) =>
                          saveEditWithText(
                            (messagesWithSeparators[virtualRow.index] as any)
                              .id,
                            text,
                          )
                      "
                      @cancel-edit="cancelEdit"
                    />
                  </div>
                </template>
              </div>
            </div>
          </template>
        </div>

        <!-- Scroll to latest button (centered over messages) -->
        <button
          v-if="hasMoreAfter"
          class="scroll-to-latest"
          aria-label="К последним сообщениям"
          @click="jumpToLatest"
        >
          <SvgIcon name="chevronDown" />
        </button>
      </div>

      <!-- Input area integrated at bottom -->
      <div class="input-wrapper">
        <div class="input-container">
          <template v-if="canSendMessages">
            <BBCodeEditor
              ref="editorRef"
              v-model="newMessage"
              context="message"
              placeholder="Написать сообщение..."
              :draft-key="composerDraftKey('chat', 'message', selectedChat?.id)"
              :disabled="sending"
              :min-height="60"
              :max-height="200"
              :resizable="true"
              :is-moderator="isModerator"
              @submit="handleSend"
            />
            <button
              class="send-button"
              :disabled="sending || !newMessage.trim()"
              @click="handleSend"
            >
              Отправить
            </button>
          </template>
        </div>
      </div>

      <!-- Fixed toolbar -->
      <Teleport to="body">
        <div
          v-if="
            hoveredMessage &&
            !hoveredMessage.isRemoved &&
            !isEditing(hoveredMessage.id)
          "
          ref="toolbarEl"
          class="msg-toolbar-fixed"
          :style="{
            top: toolbarPosition.top + 'px',
            right: toolbarPosition.right + 'px',
          }"
          @mouseenter="handleToolbarMouseEnter"
          @mouseleave="handleToolbarMouseLeave"
          @focusin="handleToolbarFocusIn"
          @focusout="handleToolbarFocusOut"
        >
          <!-- Delete confirmation mode -->
          <template v-if="confirmingDeleteId === hoveredMessage.id">
            <Tooltip text="Подтвердить удаление">
              <button
                class="toolbar-btn toolbar-btn-delete-confirm"
                aria-label="Подтвердить удаление"
                @click="confirmDelete"
              >
                <SvgIcon name="trash" />
              </button>
            </Tooltip>
            <Tooltip text="Отмена">
              <button
                class="toolbar-btn toolbar-btn-cancel"
                aria-label="Отмена"
                @click="cancelDelete"
              >
                {{ symbols.close }}
              </button>
            </Tooltip>
          </template>
          <!-- Normal mode -->
          <template v-else>
            <Tooltip
              v-if="canLikeMessage(hoveredMessage)"
              :text="isLikedByMe(hoveredMessage) ? 'Убрать лайк' : 'Нравится'"
            >
              <button
                class="toolbar-btn"
                :class="{ active: isLikedByMe(hoveredMessage) }"
                :aria-label="
                  isLikedByMe(hoveredMessage) ? 'Убрать лайк' : 'Нравится'
                "
                @click="toggleLike(hoveredMessage)"
              >
                <SvgIcon name="heartEmpty" />
              </button>
            </Tooltip>
            <Tooltip v-if="canEditMessage(hoveredMessage)" text="Редактировать">
              <button
                class="toolbar-btn"
                aria-label="Редактировать"
                @click="startEdit(hoveredMessage)"
              >
                <SvgIcon name="pencil" />
              </button>
            </Tooltip>
            <Tooltip v-if="canDeleteMessage(hoveredMessage)" text="Удалить">
              <button
                class="toolbar-btn"
                aria-label="Удалить"
                @click="requestDelete(hoveredMessage.id)"
              >
                <SvgIcon name="trash" />
              </button>
            </Tooltip>
          </template>
        </div>
      </Teleport>
    </template>

    <secondary-text v-else class="not-found">
      Переписка не найдена
    </secondary-text>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/BbcodeContent"
@import "@/assets/styles/Inputs"
@import "@/assets/styles/ZIndex"

.chat-view
  display: flex
  flex-direction: column
  min-height: 400px

.chat-header
  display: flex
  align-items: center
  gap: $medium
  padding: $small $medium
  border: 1px dashed $border
  margin-bottom: $medium

.back-button
  padding: $tiny $small
  +button

.interlocutor
  display: flex
  align-items: center
  gap: $small
  text-decoration: none

.header-avatar
  width: 40px
  height: 40px
  object-fit: cover

.username
  font-weight: 500
  color: $text
  &:hover
    color: $link-hover

.messages-wrapper
  display: flex
  flex-direction: column
  position: relative
  border: 1px dashed
  border-color: $border

.messages-container
  height: calc(100vh - 400px)
  min-height: 200px
  overflow-y: scroll
  overflow-x: hidden
  padding: 0 $small
  position: relative
  &::after
    content: ""
    display: block
    height: $medium
  &::-webkit-scrollbar
    width: 14px
  &::-webkit-scrollbar-track
    background-color: transparent
  &::-webkit-scrollbar-thumb
    background-color: $bg-element-accent
    border: 4px solid $bg-page
    border-radius: 7px

.empty-messages,
.not-found
  padding: $big

.scroll-sentinel
  width: 100%

.top-sentinel
  display: flex
  justify-content: center
  align-items: center
  min-height: 1px
  // Grows to fit the retry banner when a history-pagination request fails;
  // otherwise stays a hairline intersection target. Keyed off the banner's
  // presence rather than :empty — a v-if that renders nothing still leaves a
  // comment node behind, so the sentinel is never empty in the CSS sense.
  &:has(.sentinel-error)
    min-height: 30px
    padding: $small 0

.sentinel-error
  display: flex
  align-items: center
  gap: $small
  color: $accent-red

.sentinel-retry
  flex-shrink: 0
  +button

.date-separator
  display: flex
  align-items: center
  gap: $small
  padding: $small
  margin: $small 0
  transform: translateZ(0)
  backface-visibility: hidden

.separator-line
  flex: 1
  height: 0
  border-top: 1px dashed
  border-color: $border

.separator-text
  flex-shrink: 0
  padding: 0 $small
  font-size: $secondary-font-size
  color: $text-muted
  font-weight: 500
  white-space: nowrap

.pm-message
  padding: $small
  margin-bottom: $medium
  word-break: break-word
  overflow-wrap: break-word
  position: relative
  overflow: visible
  background-color: $bg-page
  // Force GPU compositing to prevent subpixel layout jitter
  transform: translateZ(0)
  backface-visibility: hidden

  &.continuation
    margin-bottom: $tiny
    margin-top: -$small

  &:hover,
  &.hovered
    background-color: $bg-element
    border-radius: 0 $border-radius $border-radius 0

  &:focus-visible
    background-color: $bg-element
    border-radius: 0 $border-radius $border-radius 0

  // tabindex="0" makes the whole row focusable so keyboard users can reach
  // the hover-only toolbar (focusin -> handleMessageFocusIn); outline only
  // on :focus-visible so mouse clicks don't leave a visible ring.
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: -2px

.pm-message.highlighted
  animation: highlight-pulse 1.5s ease-out forwards

.pm-message.removed
  color: $text-muted

@keyframes highlight-pulse
  0%
    box-shadow: inset 3px 0 0 0 var(--text-muted)
  50%
    box-shadow: inset 3px 0 0 0 var(--text-muted)
  100%
    box-shadow: inset 3px 0 0 0 transparent


// All msg-* header/content/reaction styles live in ChatMessage.vue (shared
// widget, scoped there). Rules for them here would be dead: parent scoped
// CSS does not reach a child component's inner DOM without :deep.

// BBCodeEditor overlay on message hover
// The overlay is layered ON TOP of the base $input-bg (does not replace it)
.pm-message:hover :deep(.bbcode-editor),
.pm-message.hovered :deep(.bbcode-editor)
  background: linear-gradient($hover-overlay, $hover-overlay), $input-bg

.input-wrapper
  flex-shrink: 0
  padding: $small
  background-color: $bg-page
  border-top: 1px dashed
  border-color: $border

// CHAT-10: same control idiom as the global chat's scroll-to-latest and the
// fixed scroll-nav buttons (24px square, 16px glyph, $border-radius — never
// a circle). Keeps a soft shadow because it floats over message content.
.scroll-to-latest
  position: absolute
  bottom: $medium
  left: 50%
  transform: translateX(-50%)
  display: flex
  align-items: center
  justify-content: center
  width: 24px
  height: 24px
  padding: 0
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  color: $text-muted
  cursor: pointer
  transition: transform 0.15s ease
  box-shadow: 0 2px 8px $shadow-color
  z-index: $z-chat-fab
  svg
    width: 16px
    height: 16px
  &:hover
    background-color: $bg-element-accent
    color: $text
    transform: translateX(-50%) scale(1.05)

.input-container
  display: flex
  flex-direction: column
  gap: $small
  width: 100%

  :deep(.bbcode-editor-wrapper)
    width: 100%

.send-button
  align-self: flex-start
  +button


// Compact display — wrapper overrides only (own elements). The compact
// header/content layout itself is handled by ChatMessage's `compact` prop.
.messages-wrapper.layout-compact
  .pm-message
    padding: $tiny $small $tiny $small
    margin-bottom: $small

    &.continuation
      margin-top: 0
</style>
