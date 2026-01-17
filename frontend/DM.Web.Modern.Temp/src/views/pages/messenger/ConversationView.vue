<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useMessagingStore, useUserStore } from "@/stores";
import { UserRole, AccessPolicy } from "@/api/models/community";
import type { ConversationId, Message } from "@/api/models/messaging";
import dayjs from "dayjs";
import defaultAvatar from "@/assets/images/userpic.png";

const route = useRoute();
const router = useRouter();
const messagingStore = useMessagingStore();
const { user: currentUser } = storeToRefs(useUserStore());
const {
  selectedConversation,
  messagesList,
  loadingConversation,
  loadingMessages,
  loadingBefore,
  sending,
  interlocutor,
  hasMoreBefore,
  hasMoreAfter,
  highlightedMessageId,
} = storeToRefs(messagingStore);

const ONLINE_THRESHOLD_MINUTES = 15;
const EDIT_TIME_LIMIT_MINUTES = 15;
const MAX_MESSAGE_HEIGHT = 200;
const CONTINUATION_TIME_LIMIT_MINUTES = 5;

const isBanned = computed(() => {
  if (!currentUser.value?.accessPolicy) return false;
  const policy = currentUser.value.accessPolicy;
  return policy === AccessPolicy.DemocraticBan || policy === AccessPolicy.FullBan;
});

const isModerator = computed(() => {
  if (!currentUser.value) return false;
  return currentUser.value.roles?.some((r: UserRole) =>
    [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(r)
  ) ?? false;
});

const canSendMessages = computed(() => currentUser.value && !isBanned.value);

const newMessage = ref("");
const messagesContainer = ref<HTMLElement | null>(null);
const inputRef = ref<HTMLTextAreaElement | null>(null);
const editInputRef = ref<HTMLTextAreaElement | null>(null);
const topSentinel = ref<HTMLElement | null>(null);
let topObserver: IntersectionObserver | null = null;

// Scroll position tracking
let isLoadingOlder = false;
let isScrolling = false;
let scrollEndTimeout: ReturnType<typeof setTimeout> | null = null;

const interlocutorPicture = computed(
  () => interlocutor.value?.smallPictureUrl || defaultAvatar
);

// Group messages with date separators
type MessageOrSeparator = (Message & { isContinuation?: boolean }) | { type: 'date-separator'; date: string; formattedDate: string };

const messagesWithSeparators = computed((): MessageOrSeparator[] => {
  if (!messagesList.value?.length) return [];

  const result: MessageOrSeparator[] = [];
  let lastDate: string | null = null;
  let lastAuthor: string | null = null;
  let lastMessageTime: dayjs.Dayjs | null = null;

  for (const msg of messagesList.value) {
    const msgDate = dayjs(msg.createdUtc).format("YYYY-MM-DD");
    const msgTime = dayjs(msg.createdUtc);

    if (lastDate !== msgDate) {
      result.push({
        type: 'date-separator',
        date: msgDate,
        formattedDate: formatSeparatorDate(msgDate)
      });
      lastDate = msgDate;
      lastAuthor = null;
      lastMessageTime = null;
    }

    const isContinuation = !!(
      !msg.isRemoved &&
      lastAuthor === msg.author?.login &&
      lastMessageTime &&
      msgTime.diff(lastMessageTime, 'minute') < CONTINUATION_TIME_LIMIT_MINUTES
    );

    result.push({ ...msg, isContinuation });

    if (!msg.isRemoved) {
      lastAuthor = msg.author?.login || null;
      lastMessageTime = msgTime;
    }
  }

  return result;
});

function formatSeparatorDate(dateStr: string): string {
  const date = dayjs(dateStr);
  const today = dayjs().startOf('day');
  const yesterday = today.subtract(1, 'day');

  if (date.isSame(today, 'day')) {
    return 'Сегодня';
  }
  if (date.isSame(yesterday, 'day')) {
    return 'Вчера';
  }
  return date.format("D MMMM YYYY");
}

function isDateSeparator(item: MessageOrSeparator): item is { type: 'date-separator'; date: string; formattedDate: string } {
  return 'type' in item && item.type === 'date-separator';
}

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
  return messagesList.value.find(m => m.id === hoveredMessageId.value) || null;
});

// Expanded messages
const expandedMessages = ref<Set<string>>(new Set());
const messagesNeedingSpoiler = ref<Set<string>>(new Set());
const expandedDeletedMessages = ref<Set<string>>(new Set());

function setupInfiniteScroll() {
  if (!messagesContainer.value) return;

  // Top sentinel - load older messages
  if (topSentinel.value) {
    topObserver = new IntersectionObserver(
      async (entries) => {
        if (!entries[0].isIntersecting || isLoadingOlder || !hasMoreBefore.value) return;
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
        threshold: 0
      }
    );
    topObserver.observe(topSentinel.value);
  }
}

function cleanupInfiniteScroll() {
  topObserver?.disconnect();
  topObserver = null;
}

async function loadConversation() {
  const id = route.params.id as ConversationId;
  await messagingStore.selectConversation(id);
  if (selectedConversation.value) {
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
    right: window.innerWidth - rect.right + 8
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

function autoGrow() {
  if (inputRef.value) {
    inputRef.value.style.height = 'auto';
    const newHeight = Math.min(inputRef.value.scrollHeight, 150);
    inputRef.value.style.height = newHeight + 'px';
  }
}

function autoGrowEdit() {
  if (editInputRef.value) {
    editInputRef.value.style.height = '0';
    editInputRef.value.style.height = editInputRef.value.scrollHeight + 'px';
  }
}

// Formatting
function formatTime(dateStr: string) {
  return dayjs(dateStr).format("HH:mm");
}

function formatFullDate(msg: Message) {
  let result = `Отправлено: ${dayjs(msg.createdUtc).format("DD.MM.YYYY HH:mm")}`;
  if (msg.modifiedUtc) {
    result += `\nОтредактировано: ${dayjs(msg.modifiedUtc).format("DD.MM.YYYY HH:mm")}`;
  }
  return result;
}

function formatDeletedDate(msg: Message) {
  let result = `Отправлено: ${dayjs(msg.createdUtc).format("DD.MM.YYYY HH:mm")}`;
  result += `\nУдалено: ${msg.modifiedUtc ? dayjs(msg.modifiedUtc).format("DD.MM.YYYY HH:mm") : "неизвестно"}`;
  return result;
}

// Track latest online status per login
const latestOnlineByLogin = computed(() => {
  const map = new Map<string, string>();
  if (!messagesList.value?.length) return map;
  for (const msg of messagesList.value) {
    if (!msg.author?.login || !msg.author?.onlineUtc) continue;
    const existing = map.get(msg.author.login);
    if (!existing || dayjs(msg.author.onlineUtc).isAfter(dayjs(existing))) {
      map.set(msg.author.login, msg.author.onlineUtc);
    }
  }
  return map;
});

function isOnline(author: any) {
  if (!author?.login) return false;
  const onlineUtc = latestOnlineByLogin.value.get(author.login);
  if (!onlineUtc) return false;
  const minutesSinceOnline = dayjs().diff(dayjs(onlineUtc), "minute", true);
  return minutesSinceOnline <= ONLINE_THRESHOLD_MINUTES;
}

// Permissions
function canEditMessage(msg: Message) {
  if (!currentUser.value || msg.isRemoved) return false;
  if (isModerator.value) return true;
  if (msg.author?.login !== currentUser.value.login) return false;
  const minutesSinceCreation = dayjs().diff(dayjs(msg.createdUtc), "minute", true);
  return minutesSinceCreation <= EDIT_TIME_LIMIT_MINUTES;
}

function canDeleteMessage(msg: Message) {
  return canEditMessage(msg);
}

function canLikeMessage(msg: Message) {
  return !!currentUser.value;
}

function isLikedByMe(msg: Message) {
  if (!currentUser.value) return false;
  return msg.likes?.some((u: any) => u.login === currentUser.value?.login) ?? false;
}

function getLikesTooltip(msg: Message) {
  if (!msg.likes?.length) return "Нравится";
  const names = msg.likes.map((u: any) => u.login);
  const count = names.length;
  if (count === 1) return `${names[0]} оценил(а) это`;
  if (count === 2) return `${names[0]} и ${names[1]} оценили это`;
  if (count <= 5) {
    const last = names.pop();
    return `${names.join(", ")} и ${last} оценили это`;
  }
  const shown = names.slice(0, 3);
  const remaining = count - 3;
  return `${shown.join(", ")} и ещё ${remaining} оценили это`;
}

// Edit
function isEditing(msgId: string) {
  return editingId.value === msgId;
}

function startEdit(msg: Message) {
  editingId.value = msg.id;
  editText.value = (msg.text || "").replace(/<br\s*\/?>/gi, "\n").replace(/<[^>]*>/g, "");
  nextTick(() => {
    requestAnimationFrame(() => {
      if (editInputRef.value) {
        editInputRef.value.focus();
        editInputRef.value.selectionStart = editInputRef.value.value.length;
        editInputRef.value.selectionEnd = editInputRef.value.value.length;
        editInputRef.value.style.height = '0';
        editInputRef.value.style.height = editInputRef.value.scrollHeight + 'px';
      }
    });
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

function handleEditKeydown(e: KeyboardEvent) {
  if (e.key === "Enter" && !e.shiftKey) {
    e.preventDefault();
    if (editingId.value) saveEdit(editingId.value);
  } else if (e.key === "Escape") {
    cancelEdit();
  }
}

// Spoiler
function needsSpoiler(msg: Message) {
  return msg.text?.includes("[cut]") || messagesNeedingSpoiler.value.has(msg.id);
}

function checkMessageHeight(msgId: string, el: HTMLElement | null) {
  if (!el || messagesNeedingSpoiler.value.has(msgId)) return;
  requestAnimationFrame(() => {
    if (el.scrollHeight > MAX_MESSAGE_HEIGHT) {
      messagesNeedingSpoiler.value.add(msgId);
    }
  });
}

function isExpanded(msgId: string) {
  return expandedMessages.value.has(msgId);
}

function isCollapsed(msg: Message) {
  return needsSpoiler(msg) && !isExpanded(msg.id);
}

function toggleExpand(msgId: string) {
  if (expandedMessages.value.has(msgId)) {
    expandedMessages.value.delete(msgId);
  } else {
    expandedMessages.value.add(msgId);
  }
  if (hoveredMessageId.value === msgId) {
    nextTick(() => {
      const msgElement = document.getElementById(`msg-${msgId}`);
      if (msgElement) {
        const rect = msgElement.getBoundingClientRect();
        toolbarPosition.value = {
          top: rect.top - 16,
          right: window.innerWidth - rect.right + 8
        };
      }
    });
  }
}

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
  if (!newMessage.value.trim() || sending.value || !selectedConversation.value) return;
  const text = newMessage.value;
  newMessage.value = "";
  if (inputRef.value) {
    inputRef.value.style.height = 'auto';
  }
  await messagingStore.sendMessage(selectedConversation.value.id, text);
  scrollToBottom();
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === "Enter" && !e.shiftKey) {
    e.preventDefault();
    handleSend();
  }
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

watch(() => route.params.id, loadConversation, { immediate: true });

onMounted(() => {
  messagesContainer.value?.addEventListener('wheel', handleWheel, { passive: true });
  messagesContainer.value?.addEventListener('scroll', handleScroll);
});

onUnmounted(() => {
  cleanupInfiniteScroll();
  messagingStore.clearSelection();
  messagesContainer.value?.removeEventListener('wheel', handleWheel);
  messagesContainer.value?.removeEventListener('scroll', handleScroll);
  if (hideToolbarTimeout) clearTimeout(hideToolbarTimeout);
  if (scrollEndTimeout) clearTimeout(scrollEndTimeout);
});
</script>

<template>
  <div class="conversation-view">
    <the-loader v-if="loadingConversation" :big="true" />

    <template v-else-if="selectedConversation">
      <div class="conversation-header">
        <button class="back-button" @click="goBack">&larr;</button>
        <router-link
          v-if="interlocutor"
          :to="{ name: 'profile', params: { login: interlocutor.login } }"
          class="interlocutor"
        >
          <img :src="interlocutorPicture" :alt="interlocutor.login" class="header-avatar" />
          <span class="username">{{ interlocutor.login }}</span>
        </router-link>
      </div>

      <div class="messages-wrapper">
        <div ref="messagesContainer" class="messages-container">
          <the-loader v-if="loadingMessages" :big="true" />

          <secondary-text v-else-if="!messagesList?.length" class="empty-messages">
            Начните переписку, отправив первое сообщение
          </secondary-text>

          <template v-else>
            <!-- Top sentinel for loading older messages -->
            <div v-if="hasMoreBefore" ref="topSentinel" class="scroll-sentinel top-sentinel">
              <the-loader v-if="loadingBefore" :small="true" />
            </div>

            <template v-for="item in messagesWithSeparators" :key="isDateSeparator(item) ? `sep-${item.date}` : item.id">
              <!-- Date Separator -->
              <div v-if="isDateSeparator(item)" class="date-separator">
                <div class="separator-line"></div>
                <span class="separator-text">{{ item.formattedDate }}</span>
                <div class="separator-line"></div>
              </div>

              <!-- Message -->
              <div
                v-else
                :id="`msg-${item.id}`"
                class="pm-message"
                :class="{ removed: item.isRemoved, hovered: hoveredMessageId === item.id, continuation: item.isContinuation }"
                @mouseenter="handleMessageMouseEnter($event, item.id)"
                @mouseleave="handleMessageMouseLeave"
              >
                <!-- Deleted message -->
                <template v-if="item.isRemoved">
                  <div v-if="!isDeletedExpanded(item.id)" class="msg-layout">
                    <div class="msg-avatar-placeholder">
                      <svg viewBox="0 0 56 56" width="56" height="56" class="deleted-avatar">
                        <circle cx="28" cy="28" r="26" fill="none" stroke="#ccc" stroke-width="1" stroke-dasharray="4 2"/>
                        <path d="M18 18l20 20M38 18l-20 20" stroke="#ccc" stroke-width="1.5"/>
                      </svg>
                    </div>
                    <div
                      class="msg-deleted"
                      :class="{ clickable: isModerator }"
                      @click="toggleDeletedExpand(item.id)"
                    >
                      <span class="msg-deleted-label">Сообщение удалено</span>
                    </div>
                  </div>
                  <div v-else class="msg-layout">
                    <router-link :to="{ name: 'profile', params: { login: item.author.login } }" class="msg-avatar-link">
                      <img :src="item.author.smallPictureUrl || defaultAvatar" :alt="item.author.login" class="msg-avatar" />
                    </router-link>
                    <div class="msg-body">
                      <div class="msg-header">
                        <router-link
                          :to="{ name: 'profile', params: { login: item.author.login } }"
                          class="msg-author"
                          :class="{ online: isOnline(item.author) }"
                        >{{ item.author.login }}</router-link>
                        <span class="msg-time-group" :title="formatDeletedDate(item)">
                          <span class="msg-time">{{ formatTime(item.createdUtc) }}</span>
                          <svg class="msg-deleted-icon" viewBox="0 0 24 24" fill="none"><path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>
                        </span>
                        <button class="msg-eye-btn" title="Скрыть" @click="toggleDeletedExpand(item.id)">
                          <svg class="eye-open" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
                          <svg class="eye-closed" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M3 12c0 0 4 5 9 5s9-5 9-5"/></svg>
                        </button>
                      </div>
                      <div class="msg-content">
                        <div class="msg-text" v-html="item.text" />
                      </div>
                    </div>
                  </div>
                </template>

                <!-- Normal message -->
                <template v-else>
                  <!-- Continuation message (compact) -->
                  <div v-if="item.isContinuation" class="msg-layout msg-continuation">
                    <div class="msg-time-gutter">
                      <span class="msg-time-hover" :title="formatFullDate(item)">{{ formatTime(item.createdUtc) }}<svg v-if="item.modifiedUtc" class="msg-edited-icon" viewBox="0 0 24 24" fill="none"><path d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg></span>
                    </div>
                    <div class="msg-body">
                      <template v-if="!isEditing(item.id)">
                        <div
                          :ref="(el) => checkMessageHeight(item.id, el as HTMLElement)"
                          class="msg-content"
                          :class="{ collapsed: isCollapsed(item) }"
                        >
                          <div class="msg-text" v-html="item.text" />
                        </div>
                        <div v-if="needsSpoiler(item)" class="msg-spoiler-row msg-spoiler-row-compact">
                          <button class="spoiler-toggle" @click="toggleExpand(item.id)">
                            <svg v-if="isExpanded(item.id)" viewBox="0 0 12 12" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                              <path d="M2 8L6 4L10 8"/>
                            </svg>
                            <svg v-else viewBox="0 0 12 12" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                              <path d="M2 4L6 8L10 4"/>
                            </svg>
                          </button>
                        </div>
                        <div v-if="item.likes?.length > 0" class="msg-reactions">
                          <button
                            class="reaction-badge"
                            :class="{ 'my-reaction': isLikedByMe(item) }"
                            :title="getLikesTooltip(item)"
                            @click="toggleLike(item)"
                          >
                            <svg viewBox="0 0 24 24" width="14" height="14" class="reaction-heart" fill="none"><path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z" stroke="currentColor" stroke-width="2"/></svg>
                            <span class="reaction-count">{{ item.likes.length }}</span>
                          </button>
                        </div>
                      </template>
                      <div v-else class="msg-edit">
                        <textarea
                          ref="editInputRef"
                          v-model="editText"
                          class="msg-edit-input"
                          @keydown="handleEditKeydown"
                          @input="autoGrowEdit"
                        />
                        <div class="msg-edit-hint">
                          escape для <a href="#" @click.prevent="cancelEdit">отмены</a> | enter для <a href="#" @click.prevent="saveEdit(item.id)">сохранения</a>
                        </div>
                      </div>
                    </div>
                  </div>

                  <!-- Full message (with avatar and header) -->
                  <div v-else class="msg-layout">
                    <router-link :to="{ name: 'profile', params: { login: item.author.login } }" class="msg-avatar-link">
                      <img :src="item.author.smallPictureUrl || defaultAvatar" :alt="item.author.login" class="msg-avatar" />
                    </router-link>

                    <div class="msg-body">
                      <div class="msg-header">
                        <router-link
                          :to="{ name: 'profile', params: { login: item.author.login } }"
                          class="msg-author"
                          :class="{ online: isOnline(item.author) }"
                        >{{ item.author.login }}</router-link>
                        <span class="msg-time-group" :title="formatFullDate(item)">
                          <span class="msg-time">{{ formatTime(item.createdUtc) }}</span><svg v-if="item.modifiedUtc" class="msg-edited-icon" viewBox="0 0 24 24" fill="none"><path d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>
                        </span>
                        <a class="msg-header-btn" title="Ссылка на сообщение" :href="`#msg-${item.id}`" @click.prevent="copyAnchor(item.id)">
                          <svg viewBox="0 0 16 16" width="14" height="14" fill="currentColor"><path d="M4.715 6.542 3.343 7.914a3 3 0 1 0 4.243 4.243l1.828-1.829A3 3 0 0 0 8.586 5.5L8 6.086a1.002 1.002 0 0 0-.154.199 2 2 0 0 1 .861 3.337L6.88 11.45a2 2 0 1 1-2.83-2.83l.793-.792a4.018 4.018 0 0 1-.128-1.287z"/><path d="M6.586 4.672A3 3 0 0 0 7.414 9.5l.775-.776a2 2 0 0 1-.896-3.346L9.12 3.55a2 2 0 1 1 2.83 2.83l-.793.792c.112.42.155.855.128 1.287l1.372-1.372a3 3 0 1 0-4.243-4.243L6.586 4.672z"/></svg>
                        </a>
                      </div>

                      <template v-if="!isEditing(item.id)">
                        <div
                          :ref="(el) => checkMessageHeight(item.id, el as HTMLElement)"
                          class="msg-content"
                          :class="{ collapsed: isCollapsed(item) }"
                        >
                          <div class="msg-text" v-html="item.text" />
                        </div>
                        <div v-if="needsSpoiler(item)" class="msg-spoiler-row">
                          <button class="spoiler-toggle" @click="toggleExpand(item.id)">
                            <svg v-if="isExpanded(item.id)" viewBox="0 0 12 12" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                              <path d="M2 8L6 4L10 8"/>
                            </svg>
                            <svg v-else viewBox="0 0 12 12" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                              <path d="M2 4L6 8L10 4"/>
                            </svg>
                          </button>
                        </div>
                        <div v-if="item.likes?.length > 0" class="msg-reactions">
                          <button
                            class="reaction-badge"
                            :class="{ 'my-reaction': isLikedByMe(item) }"
                            :title="getLikesTooltip(item)"
                            @click="toggleLike(item)"
                          >
                            <svg viewBox="0 0 24 24" width="14" height="14" class="reaction-heart" fill="none"><path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z" stroke="currentColor" stroke-width="2"/></svg>
                            <span class="reaction-count">{{ item.likes.length }}</span>
                          </button>
                        </div>
                      </template>

                      <div v-else class="msg-edit">
                        <textarea
                          ref="editInputRef"
                          v-model="editText"
                          class="msg-edit-input"
                          @keydown="handleEditKeydown"
                          @input="autoGrowEdit"
                        />
                        <div class="msg-edit-hint">
                          escape для <a href="#" @click.prevent="cancelEdit">отмены</a> | enter для <a href="#" @click.prevent="saveEdit(item.id)">сохранения</a>
                        </div>
                      </div>
                    </div>
                  </div>
                </template>
              </div>
            </template>
          </template>
        </div>

        <!-- Input area integrated at bottom -->
        <div class="input-wrapper">
          <!-- Jump to latest button -->
          <button v-if="hasMoreAfter" class="jump-to-latest" @click="jumpToLatest">
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M12 5v14M19 12l-7 7-7-7"/>
            </svg>
            К последним сообщениям
          </button>

          <div class="input-container">
            <template v-if="canSendMessages">
              <textarea
                ref="inputRef"
                v-model="newMessage"
                class="message-input"
                placeholder="Написать сообщение..."
                rows="1"
                :disabled="sending"
                @keydown="handleKeydown"
                @input="autoGrow"
              />
              <button
                class="send-button"
                :disabled="sending || !newMessage.trim()"
                @click="handleSend"
              >
                <svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round">
                  <path d="M22 12L4 2v20L22 12z"/>
                  <path d="M22 12H4M4 2l8 10-8 10"/>
                </svg>
              </button>
            </template>
            <secondary-text v-else-if="isBanned" class="banned-hint">
              Вы не можете отправлять сообщения из-за ограничений аккаунта
            </secondary-text>
          </div>
        </div>
      </div>

      <!-- Fixed toolbar -->
      <Teleport to="body">
        <div
          v-if="hoveredMessage && !hoveredMessage.isRemoved && !isEditing(hoveredMessage.id)"
          class="msg-toolbar-fixed"
          :style="{ top: toolbarPosition.top + 'px', right: toolbarPosition.right + 'px' }"
          @mouseenter="handleToolbarMouseEnter"
          @mouseleave="handleToolbarMouseLeave"
        >
          <!-- Delete confirmation mode -->
          <template v-if="confirmingDeleteId === hoveredMessage.id">
            <button class="toolbar-btn toolbar-btn-delete-confirm" title="Подтвердить удаление" @click="confirmDelete">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none"><path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>
            </button>
            <button class="toolbar-btn toolbar-btn-cancel" title="Отмена" @click="cancelDelete">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none"><path d="M18 6L6 18M6 6l12 12" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>
            </button>
          </template>
          <!-- Normal mode -->
          <template v-else>
            <button
              v-if="canLikeMessage(hoveredMessage)"
              class="toolbar-btn"
              :class="{ active: isLikedByMe(hoveredMessage) }"
              :title="isLikedByMe(hoveredMessage) ? 'Убрать лайк' : 'Нравится'"
              @click="toggleLike(hoveredMessage)"
            >
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none"><path d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z" stroke="currentColor" stroke-width="2"/></svg>
            </button>
            <button v-if="canEditMessage(hoveredMessage)" class="toolbar-btn" title="Редактировать" @click="startEdit(hoveredMessage)">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none"><path d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>
            </button>
            <button v-if="canDeleteMessage(hoveredMessage)" class="toolbar-btn" title="Удалить" @click="requestDelete(hoveredMessage.id)">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none"><path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>
            </button>
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

.conversation-view
  display: flex
  flex-direction: column
  height: calc(100vh - 250px)
  min-height: 400px

.conversation-header
  display: flex
  align-items: center
  gap: $medium
  padding: $medium
  +theme(background-color, $panel-background)
  border-radius: $border-radius
  margin-bottom: $medium

.back-button
  padding: $small $medium
  border: none
  border-radius: $border-radius
  cursor: pointer
  font-size: $title-font-size
  +theme(background-color, $control-background)
  +theme(color, $text)

  &:hover
    +theme(background-color, $panel-background-highlight)

.interlocutor
  display: flex
  align-items: center
  gap: $small
  text-decoration: none

.header-avatar
  width: $grid-step * 10
  height: $grid-step * 10
  border-radius: 50%
  object-fit: cover

.username
  font-weight: bold
  +theme(color, $active-text)

  &:hover
    +theme(color, $active-text-hover)

.messages-wrapper
  flex: 1
  display: flex
  flex-direction: column
  position: relative
  border: 1px dashed
  +theme(border-color, $border)
  min-height: 0

.messages-container
  flex: 1
  overflow-y: scroll
  overflow-x: hidden
  padding: 0 $small
  margin-bottom: 58px
  &::after
    content: ""
    display: block
    height: $medium
  position: relative
  &::-webkit-scrollbar
    width: 16px
  &::-webkit-scrollbar-track
    +theme(background-color, $background)
    border: 4px solid
    border-left-width: 2px
    +theme(border-color, $background)
    margin-bottom: 12px
  &::-webkit-scrollbar-thumb
    +theme(background-color, $control-background)
    border-radius: $border-radius
    border: 4px solid
    border-left-width: 2px
    +theme(border-color, $background)

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

.separator-line
  flex: 1
  height: 0
  border-top: 1px dashed
  +theme(border-color, $border)

.separator-text
  flex-shrink: 0
  padding: 0 $small
  font-size: $secondary-font-size
  color: #888
  font-weight: 500
  white-space: nowrap

.pm-message
  padding: $small
  margin-bottom: $medium
  word-break: break-word
  overflow-wrap: break-word
  position: relative
  overflow: visible
  +theme(background-color, $background)

  &.continuation
    margin-bottom: $tiny
    margin-top: -$small

  &:hover,
  &.hovered
    +theme(background-color, $panel-background)
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
      +theme(color, $secondary-text)
      opacity: 0
      transition: opacity 0.1s ease
      height: 24px
      display: flex
      align-items: flex-end
      position: relative
      top: -0.5px
      .msg-edited-icon
        margin-left: 3px
        margin-bottom: 4px

.pm-message:hover .msg-time-gutter .msg-time-hover,
.pm-message.hovered .msg-time-gutter .msg-time-hover
  opacity: 1

.msg-spoiler-row-compact
  margin-left: 0
  width: 100%

.pm-message.highlighted
  animation: highlight-pulse 1.5s ease-out forwards

.pm-message.removed
  +theme(color, $secondary-text)

@keyframes highlight-pulse
  0%
    box-shadow: inset 3px 0 0 0 #888
  50%
    box-shadow: inset 3px 0 0 0 #888
  100%
    box-shadow: inset 3px 0 0 0 transparent

.msg-layout
  display: flex
  gap: $medium

.msg-body
  flex: 1
  min-width: 0
  position: relative

.msg-header
  display: flex
  align-items: center
  gap: $medium
  margin-bottom: $tiny
  min-height: 20px

.msg-avatar-link
  flex-shrink: 0

.msg-avatar
  width: 56px
  height: 56px
  border-radius: 50%
  object-fit: cover
  display: block

.msg-avatar-placeholder
  flex-shrink: 0
  width: 56px
  height: 56px

.deleted-avatar
  border-radius: 50%

.msg-author
  text-decoration: none
  font-weight: 500
  +theme(color, $secondary-text)
  &:hover
    +theme(color, $secondary-text-hover)
    text-decoration: underline
  &.online
    +theme(color, $link-online)
    &:hover
      +theme(color, $positive-text-hover)
      text-decoration: underline

.msg-time-group
  display: inline-flex
  align-items: baseline
  gap: 6px
  +theme(color, $secondary-text)

.msg-edited-icon
  width: 14px
  height: 14px
  margin-left: -3px
  flex-shrink: 0
  position: relative
  top: 2px

.msg-header-btn,
.msg-eye-btn
  display: inline-flex
  align-items: center
  justify-content: center
  padding: 0
  border: none
  background: transparent
  cursor: pointer
  +theme(color, $secondary-text)
  position: relative
  top: 1px
  svg
    width: 14px
    height: 14px
  &:hover
    filter: brightness(0.7)

.msg-eye-btn
  position: relative
  top: 0
  .eye-open
    display: block
  .eye-closed
    display: none
  &:hover
    filter: none
    .eye-open
      display: none
    .eye-closed
      display: block

.msg-content
  +theme(color, $text)
  line-height: 1.5
  position: relative

  &.collapsed
    display: -webkit-box
    -webkit-line-clamp: 10
    -webkit-box-orient: vertical
    overflow: hidden
    &::after
      content: ""
      position: absolute
      bottom: 0
      left: 0
      right: 0
      height: 1.5em
      background: linear-gradient(to bottom, transparent, var(--bg-page))
      pointer-events: none

.pm-message:hover .msg-content.collapsed::after,
.pm-message.hovered .msg-content.collapsed::after
  background: linear-gradient(to bottom, transparent, var(--bg-element))

.msg-text
  :deep(p)
    margin: 0
    &:not(:last-child)
      margin-bottom: $tiny

.msg-edit
  margin-top: 0

.msg-edit-input
  display: block
  width: 100%
  padding: $small
  border: 1px dashed
  border-radius: $border-radius
  +theme(border-color, $border)
  +theme(background-color, $panel-background)
  +theme(color, $text)
  font-family: inherit
  font-size: inherit
  line-height: 1.5
  resize: none
  box-sizing: border-box
  overflow: hidden
  outline: none
  field-sizing: content
  &:focus
    outline: none
    box-shadow: none

.msg-edit-hint
  font-size: $secondary-font-size
  +theme(color, $secondary-text)
  margin-top: $tiny
  a
    +theme(color, $link)
    text-decoration: none
    &:hover
      +theme(color, $link-hover)
      text-decoration: underline

.msg-spoiler-row
  display: flex
  justify-content: center
  margin-top: $small
  margin-left: calc(-40px - #{$small})
  width: calc(100% + 40px + #{$small})

.spoiler-toggle
  display: inline-flex
  align-items: center
  justify-content: center
  padding: 2px 6px
  border: none
  background: transparent
  +theme(color, $secondary-text)
  cursor: pointer
  &:hover
    filter: brightness(0.7)

.msg-reactions
  display: flex
  align-items: center
  gap: $tiny
  margin-top: $small

.msg-spoiler-row + .msg-reactions
  margin-top: -8px

:global(.msg-toolbar-fixed)
  position: fixed
  display: flex
  align-items: center
  gap: 0
  padding: 0
  +theme(background-color, $panel-background)
  box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.08), 0 2px 8px rgba(0, 0, 0, 0.12)
  border-radius: $border-radius
  z-index: 10000

:global(.toolbar-btn-delete-confirm)
  color: #c44 !important
  &:hover
    color: #a33 !important

:global(.toolbar-btn-cancel)
  color: #888 !important
  &:hover
    color: #666 !important

:global(.toolbar-btn)
  display: flex
  align-items: center
  justify-content: center
  width: 36px
  height: 32px
  padding: 0
  border: none
  border-radius: 0
  background: transparent
  cursor: pointer
  +theme(color, $secondary-text)
  transition: background-color 0.1s ease, color 0.1s ease

:global(.toolbar-btn svg)
  transition: transform 0.15s ease

:global(.toolbar-btn:hover)
  +theme(background-color, $control-background)
  +theme(color, $text)

:global(.toolbar-btn:hover svg)
  transform: scale(1.15)

:global(.toolbar-btn:active)
  background-color: rgba(0, 0, 0, 0.04)

:global(.toolbar-btn:active svg)
  transform: scale(0.9)

:global(.toolbar-btn.active)
  +theme(color, $secondary-text)

:global(.toolbar-btn.active svg)
  fill: currentColor

:global(.toolbar-btn.active:hover)
  +theme(color, $text)
  +theme(background-color, $control-background)

.reaction-badge
  display: inline-flex
  align-items: center
  gap: 6px
  padding: 6px 10px
  border: none
  background-color: rgba(0, 0, 0, 0.05)
  +theme(color, $secondary-text)
  cursor: pointer
  transition: background-color 0.1s ease, color 0.1s ease, transform 0.1s ease
  border-radius: $border-radius
  svg
    width: 18px
    height: 18px
    fill: none
    transition: transform 0.15s ease
  &:hover
    background-color: rgba(0, 0, 0, 0.1)
    +theme(color, $text)
    svg
      transform: scale(1.15)
  &:active
    background-color: rgba(0, 0, 0, 0.04)
    transform: scale(0.95)
  &.my-reaction
    svg
      fill: currentColor

.pm-message:hover .reaction-badge,
.pm-message.hovered .reaction-badge
  background-color: rgba(0, 0, 0, 0.06)

.pm-message:hover .reaction-badge:hover,
.pm-message.hovered .reaction-badge:hover
  background-color: rgba(0, 0, 0, 0.14)

.pm-message:hover .reaction-badge.my-reaction,
.pm-message.hovered .reaction-badge.my-reaction
  background-color: rgba(0, 0, 0, 0.12)

.pm-message:hover .reaction-badge.my-reaction:hover,
.pm-message.hovered .reaction-badge.my-reaction:hover
  background-color: rgba(0, 0, 0, 0.18)

.reaction-count
  color: inherit
  font-size: $secondary-font-size
  font-weight: 500

.msg-deleted
  display: flex
  flex-direction: column
  justify-content: center
  min-height: 40px
  +theme(color, $secondary-text)
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
  +theme(color, $secondary-text)

.input-wrapper
  position: absolute
  bottom: 0
  left: 0
  right: 0
  z-index: 50
  padding: 0 $small $small $small

.jump-to-latest
  position: absolute
  bottom: 100%
  left: 50%
  transform: translateX(-50%)
  margin-bottom: $medium
  display: flex
  align-items: center
  gap: $tiny
  padding: $small $medium
  border: 1px dashed
  border-radius: $border-radius
  +theme(border-color, $border)
  +theme(background-color, $background)
  color: #666 !important
  cursor: pointer
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15)
  font-family: inherit
  font-size: $font-size
  white-space: nowrap
  transition: transform 0.15s ease, background-color 0.15s ease
  svg
    stroke: #666
    transition: stroke 0.15s ease
  &:hover
    +theme(background-color, $panel-background)
    color: #444 !important
    transform: translateX(-50%) scale(1.02)
    svg
      stroke: #444

.input-container
  display: flex
  align-items: flex-end
  gap: $small
  padding: $small
  border: 1px dashed
  border-radius: $border-radius
  +theme(border-color, $border)
  +theme(background-color, $panel-background)

.message-input
  flex: 1
  padding: $small
  border: none
  border-radius: 0
  background: transparent
  +theme(color, $text)
  resize: none
  font-family: inherit
  font-size: inherit
  line-height: 1.4
  box-sizing: border-box
  min-height: 24px
  max-height: 150px
  overflow-y: auto
  outline: none
  box-shadow: none
  &:focus
    outline: none
    box-shadow: none
  &::placeholder
    +theme(color, $secondary-text)

.send-button
  flex-shrink: 0
  display: flex
  align-items: center
  justify-content: center
  width: 36px
  height: 36px
  padding: 0
  background: transparent
  border: none
  cursor: pointer
  +theme(color, $secondary-text)
  transition: color 0.15s ease
  &:hover:not(:disabled)
    color: #666
  &:disabled
    opacity: 0.3
    cursor: not-allowed

.banned-hint
  flex: 1
  text-align: center
  padding: $small
  +theme(color, $negative-text)
</style>
