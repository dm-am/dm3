<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useMessagingStore } from "@/entities/message";
import { useUserStore } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import { AccessPolicy } from "@/shared/api/models/community";
import {
  groupMessagesWithSeparators,
  isDateSeparator,
  getLikesTooltip as getLikesTooltipUtil,
  isUserOnline,
  type MessageOrSeparator,
} from "@/shared/lib/utils/chat";
import {
  useMessagePermissions,
  useVirtualScroll,
} from "@/shared/lib/composables";
import { ChatMessage } from "@/widgets/chat-message";
import { Tooltip } from "@/shared/ui/Tooltip";
import type { ChatId, Message } from "@/entities/message";
import dayjs from "dayjs";
import { symbols } from "@/shared/lib/utils/icons";
import { AvatarImg } from "@/entities/user";
import { SvgIcon } from "@/shared/ui/Icon";
import { BBCodeEditor } from "@/features/editor";
import { messagingApi } from "@/entities/message";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";

const route = useRoute();
const router = useRouter();
const messagingStore = useMessagingStore();
const { user: currentUser } = storeToRefs(useUserStore());
const { isCompactLayout } = storeToRefs(useUiStore());
const {
  selectedChat,
  messagesList,
  loadingChat,
  loadingMessages,
  loadingBefore,
  sending,
  interlocutor,
  hasMoreBefore,
  hasMoreAfter,
} = storeToRefs(messagingStore);

const MAX_MESSAGE_HEIGHT = 200;

const isBanned = computed(() => {
  if (!currentUser.value?.accessPolicy) return false;
  const policy = currentUser.value.accessPolicy;
  return (
    policy === AccessPolicy.DemocraticBan || policy === AccessPolicy.FullBan
  );
});

// Message permissions (shared composable)
const {
  isModerator,
  canEdit: canEditMsg,
  canDelete: canDeleteMsg,
  canLike: canLikeMsg,
} = useMessagePermissions(currentUser);

const canSendMessages = computed(() => currentUser.value && !isBanned.value);

const newMessage = ref("");
const messagesContainer = ref<HTMLElement | null>(null);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const editEditorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const topSentinel = ref<HTMLElement | null>(null);
let topObserver: IntersectionObserver | null = null;

// Scroll position tracking
let isLoadingOlder = false;
let isScrolling = false;
let scrollEndTimeout: ReturnType<typeof setTimeout> | null = null;

// Group messages with date separators (shared utility)
const messagesWithSeparators = computed((): MessageOrSeparator[] =>
  groupMessagesWithSeparators(messagesList.value ?? []),
);
// isDateSeparator imported from shared/lib/utils/chat

// Virtual scroll
const itemCount = computed(() => messagesWithSeparators.value.length);
const { virtualItems, totalSize, measureElement } = useVirtualScroll({
  count: itemCount,
  container: messagesContainer,
  estimateSize: 80,
  overscan: 15,
});

// Edit state
const editingId = ref<string | null>(null);
const editText = ref("");

// Delete confirmation state
const confirmingDeleteId = ref<string | null>(null);

// Hover toolbar state
const hoveredMessageId = ref<string | null>(null);
const toolbarPosition = ref({ top: 0, right: 0 });
const isToolbarHovered = ref(false);
let hideToolbarTimeout: ReturnType<typeof setTimeout> | null = null;

const hoveredMessage = computed(() => {
  if (!hoveredMessageId.value) return null;
  return (
    messagesList.value.find((m) => m.id === hoveredMessageId.value) || null
  );
});

// Expanded messages
const expandedDeletedMessages = ref<Set<string>>(new Set());

function setupInfiniteScroll() {
  if (!messagesContainer.value) return;

  // Top sentinel - load older messages
  if (topSentinel.value) {
    topObserver = new IntersectionObserver(
      async (entries) => {
        if (
          !entries[0].isIntersecting ||
          isLoadingOlder ||
          !hasMoreBefore.value
        )
          return;
        isLoadingOlder = true;

        const container = messagesContainer.value;
        if (!container) {
          isLoadingOlder = false;
          return;
        }

        const scrollHeightBefore = container.scrollHeight;
        await messagingStore.fetchMoreBefore();

        nextTick(() => {
          if (container) {
            const scrollHeightAfter = container.scrollHeight;
            const heightDiff = scrollHeightAfter - scrollHeightBefore;
            container.scrollTop = heightDiff;
          }
          isLoadingOlder = false;
        });
      },
      {
        root: messagesContainer.value,
        rootMargin: "100px 0px 0px 0px",
        threshold: 0,
      },
    );
    topObserver.observe(topSentinel.value);
  }
}

function cleanupInfiniteScroll() {
  topObserver?.disconnect();
  topObserver = null;
}

async function loadChat() {
  const id = route.params.id as ChatId;
  await messagingStore.selectChat(id);
  if (selectedChat.value) {
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

function handleMessageMouseEnter(event: MouseEvent, msgId: string) {
  if (isScrolling) return;
  if (hideToolbarTimeout) {
    clearTimeout(hideToolbarTimeout);
    hideToolbarTimeout = null;
  }

  const target = event.currentTarget as HTMLElement;
  const rect = target.getBoundingClientRect();

  toolbarPosition.value = {
    top: rect.top - 16,
    right: window.innerWidth - rect.right + 8,
  };
  hoveredMessageId.value = msgId;
}

function handleMessageMouseLeave() {
  if (hideToolbarTimeout) {
    clearTimeout(hideToolbarTimeout);
  }
  hideToolbarTimeout = setTimeout(() => {
    if (!isToolbarHovered.value) {
      hoveredMessageId.value = null;
      confirmingDeleteId.value = null;
    }
    hideToolbarTimeout = null;
  }, 150);
}

function handleToolbarMouseEnter() {
  if (hideToolbarTimeout) {
    clearTimeout(hideToolbarTimeout);
    hideToolbarTimeout = null;
  }
  isToolbarHovered.value = true;
}

function handleToolbarMouseLeave() {
  isToolbarHovered.value = false;
  hideToolbarTimeout = setTimeout(() => {
    hoveredMessageId.value = null;
    confirmingDeleteId.value = null;
    hideToolbarTimeout = null;
  }, 100);
}

function handleWheel() {
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

// autoGrowEdit removed - BBCodeEditor handles its own sizing

// Formatting
function formatTime(dateStr: string) {
  return dayjs(dateStr).format("HH:mm");
}

function formatFullDate(msg: Message) {
  let result = `Отправлено: ${dayjs(msg.createdUtc).format("DD.MM.YYYY [в] HH:mm")}`;
  if (msg.modifiedUtc) {
    result += `\nОтредактировано: ${dayjs(msg.modifiedUtc).format("DD.MM.YYYY [в] HH:mm")}`;
  }
  return result;
}

function formatDeletedDate(msg: Message) {
  let result = `Отправлено: ${dayjs(msg.createdUtc).format("DD.MM.YYYY [в] HH:mm")}`;
  result += `\nУдалено: ${msg.modifiedUtc ? dayjs(msg.modifiedUtc).format("DD.MM.YYYY [в] HH:mm") : "неизвестно"}`;
  return result;
}

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

function getMsgLikesTooltip(msg: Message) {
  return getLikesTooltipUtil(msg.likes ?? []);
}

// Edit
function isEditing(msgId: string) {
  return editingId.value === msgId;
}

async function startEdit(msg: Message) {
  editingId.value = msg.id;
  // Fetch the original BBCode from the backend
  const { data } = await messagingApi.getMessageForEdit(msg.id);
  if (data) {
    editText.value = data.text || "";
  } else {
    editText.value = "";
  }
  nextTick(() => {
    editEditorRef.value?.focus();
  });
}

function cancelEdit() {
  editingId.value = null;
  editText.value = "";
}

async function saveEdit(msgId: string) {
  if (editText.value.trim()) {
    await messagingStore.updateMessage(msgId, editText.value);
  }
  cancelEdit();
}

function handleEditSubmit() {
  if (editingId.value) {
    saveEdit(editingId.value);
  }
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

// Anchor
function copyAnchor(msgId: string) {
  const url = `${window.location.origin}${window.location.pathname}#msg-${msgId}`;
  navigator.clipboard.writeText(url);
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
  editorRef.value?.clear();
  await messagingStore.sendMessage(selectedChat.value.id, text);
  scrollToBottom();
}

function requestDelete(id: string) {
  confirmingDeleteId.value = id;
}

function cancelDelete() {
  confirmingDeleteId.value = null;
}

async function confirmDelete() {
  if (confirmingDeleteId.value) {
    await messagingStore.deleteMessage(confirmingDeleteId.value);
    confirmingDeleteId.value = null;
  }
}

function goBack() {
  router.push({ name: "messenger" });
}

watch(() => route.params.id, loadChat, { immediate: true });

// Watch for new messages to init interactive BBCode elements
watch(
  messagesList,
  () => {
    nextTick(() => {
      initBbcodeInteractive(messagesContainer.value);
    });
  },
  { deep: true },
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
  cleanupInfiniteScroll();
  messagingStore.clearSelection();
  messagesContainer.value?.removeEventListener("wheel", handleWheel);
  messagesContainer.value?.removeEventListener("scroll", handleScroll);
  if (hideToolbarTimeout) clearTimeout(hideToolbarTimeout);
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
            ></div>

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
                        saveEdit(
                          (messagesWithSeparators[virtualRow.index] as any).id,
                        )
                      "
                      @cancel-edit="cancelEdit"
                      @update:edit-text="editText = $event"
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
              :draft-key="`chat_${selectedChat?.id}`"
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
          <secondary-text v-else-if="isBanned" class="banned-hint">
            Вы не можете отправлять сообщения из-за ограничений аккаунта
          </secondary-text>
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
          class="msg-toolbar-fixed"
          :style="{
            top: toolbarPosition.top + 'px',
            right: toolbarPosition.right + 'px',
          }"
          @mouseenter="handleToolbarMouseEnter"
          @mouseleave="handleToolbarMouseLeave"
        >
          <!-- Delete confirmation mode -->
          <template v-if="confirmingDeleteId === hoveredMessage.id">
            <Tooltip text="Подтвердить удаление">
              <button
                class="toolbar-btn toolbar-btn-delete-confirm"
                @click="confirmDelete"
              >
                <SvgIcon name="trash" />
              </button>
            </Tooltip>
            <Tooltip text="Отмена">
              <button
                class="toolbar-btn toolbar-btn-cancel"
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
                @click="toggleLike(hoveredMessage)"
              >
                <SvgIcon name="heartEmpty" />
              </button>
            </Tooltip>
            <Tooltip v-if="canEditMessage(hoveredMessage)" text="Редактировать">
              <button class="toolbar-btn" @click="startEdit(hoveredMessage)">
                <SvgIcon name="pencil" />
              </button>
            </Tooltip>
            <Tooltip v-if="canDeleteMessage(hoveredMessage)" text="Удалить">
              <button
                class="toolbar-btn"
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
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"
@import "src/assets/styles/Inputs"
@import "src/assets/styles/ZIndex"

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
  text-align: center
  padding: $big

.scroll-sentinel
  height: 1px
  width: 100%

.top-sentinel
  display: flex
  justify-content: center
  align-items: center
  min-height: 30px
  &:empty
    min-height: 1px

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

.msg-continuation
  .msg-time-gutter
    width: 56px
    flex-shrink: 0
    display: flex
    align-items: flex-start
    justify-content: center
    .msg-time-hover
      font-size: 14px
      color: $text-muted
      opacity: 0
      transition: opacity 0.1s ease
      height: 24px
      display: flex
      align-items: flex-end
      .msg-edited-icon
        margin-left: 3px
        margin-bottom: 4px

.pm-message:hover .msg-time-gutter .msg-time-hover,
.pm-message.hovered .msg-time-gutter .msg-time-hover
  opacity: 1

.msg-expand-row-compact
  margin-left: 0
  width: 100%

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


// msg-* standalone styles moved to ChatMessage.vue

.pm-message:hover .msg-content.collapsed::after,
.pm-message.hovered .msg-content.collapsed::after
  background: linear-gradient(to bottom, transparent, var(--bg-element))


.pm-message:hover .reaction-badge,
.pm-message.hovered .reaction-badge
  background-color: $hover-overlay

.pm-message:hover .reaction-badge:hover,
.pm-message.hovered .reaction-badge:hover
  background-color: $active-overlay

.pm-message:hover .reaction-badge.my-reaction,
.pm-message.hovered .reaction-badge.my-reaction
  background-color: $active-overlay

.pm-message:hover .reaction-badge.my-reaction:hover,
.pm-message.hovered .reaction-badge.my-reaction:hover
  background-color: $active-overlay

// BBCodeEditor overlay при hover на сообщение
// Overlay накладывается ПОВЕРХ базового $input-bg (не заменяет)
.pm-message:hover :deep(.bbcode-editor),
.pm-message.hovered :deep(.bbcode-editor)
  background: linear-gradient($hover-overlay, $hover-overlay), $input-bg

.reaction-count
  color: inherit
  font-size: $secondary-font-size
  font-weight: 500

.msg-deleted
  display: flex
  flex-direction: column
  justify-content: center
  min-height: 40px
  color: $text-muted
  &.clickable
    cursor: pointer
    &:hover .msg-deleted-label
      text-decoration: underline

.msg-deleted-label
  font-style: italic

.msg-deleted-icon
  width: 14px
  height: 14px
  margin-left: -3px
  flex-shrink: 0
  position: relative
  top: 1px
  color: $text-muted

.input-wrapper
  flex-shrink: 0
  padding: $small
  background-color: $bg-page
  border-top: 1px dashed
  border-color: $border

.scroll-to-latest
  position: absolute
  bottom: $medium
  left: 50%
  transform: translateX(-50%)
  display: flex
  align-items: center
  justify-content: center
  width: 40px
  height: 40px
  border: 1px solid $border
  border-radius: 50%
  background-color: $bg-element
  color: $text-muted
  cursor: pointer
  transition: transform 0.15s ease
  box-shadow: 0 2px 8px $shadow-color
  z-index: 10
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

.banned-hint
  flex: 1
  text-align: center
  padding: $small
  color: $accent-red

// Compact display styles
.messages-wrapper.layout-compact
  .pm-message
    padding: $tiny $small $tiny $small
    margin-bottom: $small

    &.continuation
      margin-top: 0

  // Hide avatars in compact layout
  .msg-avatar-link,
  .msg-avatar-placeholder
    display: none

  // Messages layout becomes inline
  .msg-layout
    display: block

  .msg-body
    display: block

  .msg-header
    display: inline-flex
    align-items: center
    gap: $small
    margin-bottom: 0
    line-height: 1

  // Time group with fixed width for alignment
  .msg-time-group
    display: inline-flex
    align-items: center
    gap: 6px
    margin-right: 0
    min-width: 62px

  .msg-author
    display: inline-flex
    align-items: center

  // Content on new line, aligned with author name
  .msg-content
    display: block
    margin-left: calc(62px + #{$small})
    margin-top: 0

  // Edit form also aligned with content
  .msg-edit
    margin-left: calc(62px + #{$small})

  .msg-text
    display: block

  // Reactions on new line
  .msg-reactions
    display: block
    margin-top: $tiny
    margin-left: calc(62px + #{$small})

  .msg-expand-row
    display: flex
    margin-top: $tiny
    margin-left: 0
    width: 100%

  // Continuation messages in compact layout
  .msg-continuation
    display: block

    .msg-time-gutter
      display: inline-flex
      width: 62px
      justify-content: flex-start

    .msg-body
      display: block

  // Deleted messages compact
  .msg-deleted
    display: inline-flex
    min-height: auto
</style>
