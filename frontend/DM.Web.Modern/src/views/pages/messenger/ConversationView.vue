<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useMessagingStore, useUserStore } from "@/stores";
import { UserRole, AccessPolicy } from "@/api/models/community";
import type { ConversationId, Message } from "@/api/models/messaging";
import dayjs from "dayjs";
import defaultAvatar from "@/assets/images/userpic.png";
import BBCodeEditor from "@/components/inputs/BBCodeEditor.vue";
import messagingApi from "@/api/requests/messagingApi";
import { initBbcodeInteractive } from "@/utils/bbcodeInteractive";

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
} = storeToRefs(messagingStore);

const ONLINE_THRESHOLD_MINUTES = 5;
const EDIT_TIME_LIMIT_MINUTES = 15;
const MAX_MESSAGE_HEIGHT = 200;
const CONTINUATION_TIME_LIMIT_MINUTES = 5;

const isBanned = computed(() => {
  if (!currentUser.value?.accessPolicy) return false;
  const policy = currentUser.value.accessPolicy;
  return (
    policy === AccessPolicy.DemocraticBan || policy === AccessPolicy.FullBan
  );
});

const isModerator = computed(() => {
  if (!currentUser.value) return false;
  return (
    currentUser.value.roles?.some((r: UserRole) =>
      [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(
        r,
      ),
    ) ?? false
  );
});

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

const interlocutorPicture = computed(
  () => interlocutor.value?.smallPictureUrl || defaultAvatar,
);

// Group messages with date separators
type MessageOrSeparator =
  | (Message & { isContinuation?: boolean })
  | { type: "date-separator"; date: string; formattedDate: string };

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
        type: "date-separator",
        date: msgDate,
        formattedDate: formatSeparatorDate(msgDate),
      });
      lastDate = msgDate;
      lastAuthor = null;
      lastMessageTime = null;
    }

    const isContinuation = !!(
      !msg.isRemoved &&
      lastAuthor === msg.author?.login &&
      lastMessageTime &&
      msgTime.diff(lastMessageTime, "minute") < CONTINUATION_TIME_LIMIT_MINUTES
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
  const today = dayjs().startOf("day");
  const yesterday = today.subtract(1, "day");

  if (date.isSame(today, "day")) {
    return "Сегодня";
  }
  if (date.isSame(yesterday, "day")) {
    return "Вчера";
  }
  return date.format("D MMMM YYYY");
}

function isDateSeparator(
  item: MessageOrSeparator,
): item is { type: "date-separator"; date: string; formattedDate: string } {
  return "type" in item && item.type === "date-separator";
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
  return (
    messagesList.value.find((m) => m.id === hoveredMessageId.value) || null
  );
});

// Expanded messages
const expandedMessages = ref<Set<string>>(new Set());
const truncatedMessages = ref<Set<string>>(new Set());
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

// Track latest activity per login
const latestActivityByLogin = computed(() => {
  const map = new Map<string, string>();
  if (!messagesList.value?.length) return map;
  for (const msg of messagesList.value) {
    if (!msg.author?.login || !msg.author?.lastActivityUtc) continue;
    const existing = map.get(msg.author.login);
    if (!existing || dayjs(msg.author.lastActivityUtc).isAfter(dayjs(existing))) {
      map.set(msg.author.login, msg.author.lastActivityUtc);
    }
  }
  return map;
});

function isOnline(author: any) {
  if (!author?.login) return false;
  const lastActivityUtc = latestActivityByLogin.value.get(author.login);
  if (!lastActivityUtc) return false;
  const minutesSinceOnline = dayjs().diff(dayjs(lastActivityUtc), "minute", true);
  return minutesSinceOnline <= ONLINE_THRESHOLD_MINUTES;
}

// Permissions
function canEditMessage(msg: Message) {
  if (!currentUser.value || msg.isRemoved) return false;
  if (isModerator.value) return true;
  if (msg.author?.login !== currentUser.value.login) return false;
  const minutesSinceCreation = dayjs().diff(
    dayjs(msg.createdUtc),
    "minute",
    true,
  );
  return minutesSinceCreation <= EDIT_TIME_LIMIT_MINUTES;
}

function canDeleteMessage(msg: Message) {
  return canEditMessage(msg);
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
function canLikeMessage(_msg: Message) {
  return !!currentUser.value;
}

function isLikedByMe(msg: Message) {
  if (!currentUser.value) return false;
  return (
    msg.likes?.some((u: any) => u.login === currentUser.value?.login) ?? false
  );
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
  return `${shown.join(", ")} и еще ${remaining} оценили это`;
}

// Edit
function isEditing(msgId: string) {
  return editingId.value === msgId;
}

async function startEdit(msg: Message) {
  editingId.value = msg.id;
  // Fetch the original BBCode from the backend
  const { data } = await messagingApi.getMessageForEdit(msg.id);
  if (data?.resource) {
    editText.value = data.resource.text || "";
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

// Message truncation (long messages)
function needsTruncation(msg: Message) {
  return msg.text?.includes("[cut]") || truncatedMessages.value.has(msg.id);
}

function checkMessageHeight(msgId: string, el: HTMLElement | null) {
  if (!el || truncatedMessages.value.has(msgId)) return;
  requestAnimationFrame(() => {
    if (el.scrollHeight > MAX_MESSAGE_HEIGHT) {
      truncatedMessages.value.add(msgId);
    }
  });
}

function isExpanded(msgId: string) {
  return expandedMessages.value.has(msgId);
}

function isTruncated(msg: Message) {
  return needsTruncation(msg) && !isExpanded(msg.id);
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
          right: window.innerWidth - rect.right + 8,
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
  if (!newMessage.value.trim() || sending.value || !selectedConversation.value)
    return;
  const text = newMessage.value;
  newMessage.value = "";
  editorRef.value?.clear();
  await messagingStore.sendMessage(selectedConversation.value.id, text);
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

watch(() => route.params.id, loadConversation, { immediate: true });

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
  <div class="conversation-view">
    <template v-if="selectedConversation">
      <page-title v-if="interlocutor">Переписка с {{ interlocutor.login }}</page-title>
      <page-title v-else>Переписка</page-title>

      <div class="conversation-header">
        <button class="back-button" @click="goBack">&larr; Назад</button>
        <router-link
          v-if="interlocutor"
          :to="{ name: 'profile', params: { login: interlocutor.login } }"
          class="interlocutor"
        >
          <img
            :src="interlocutorPicture"
            :alt="interlocutor.login"
            class="header-avatar"
          />
          <span class="username">{{ interlocutor.login }}</span>
        </router-link>
        <span v-else class="username">Загрузка...</span>
      </div>

      <div class="messages-wrapper">
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
            </div>

            <template
              v-for="item in messagesWithSeparators"
              :key="isDateSeparator(item) ? `sep-${item.date}` : item.id"
            >
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
                :class="{
                  removed: item.isRemoved,
                  hovered: hoveredMessageId === item.id,
                  continuation: item.isContinuation,
                }"
                @mouseenter="handleMessageMouseEnter($event, item.id)"
                @mouseleave="handleMessageMouseLeave"
              >
                <!-- Deleted message -->
                <template v-if="item.isRemoved">
                  <div v-if="!isDeletedExpanded(item.id)" class="msg-layout">
                    <div class="msg-avatar-placeholder">
                      <svg
                        viewBox="0 0 56 56"
                        width="56"
                        height="56"
                        class="deleted-avatar"
                      >
                        <circle
                          cx="28"
                          cy="28"
                          r="26"
                          fill="none"
                          stroke="currentColor"
                          stroke-width="1"
                          stroke-dasharray="4 2"
                        />
                        <path
                          d="M18 18l20 20M38 18l-20 20"
                          stroke="currentColor"
                          stroke-width="1.5"
                        />
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
                    <router-link
                      :to="{
                        name: 'profile',
                        params: { login: item.author.login },
                      }"
                      class="msg-avatar-link"
                    >
                      <img
                        :src="item.author.smallPictureUrl || defaultAvatar"
                        :alt="item.author.login"
                        class="msg-avatar"
                      />
                    </router-link>
                    <div class="msg-body">
                      <div class="msg-header">
                        <router-link
                          :to="{
                            name: 'profile',
                            params: { login: item.author.login },
                          }"
                          class="msg-author"
                          :class="{ online: isOnline(item.author) }"
                          >{{ item.author.login }}</router-link
                        >
                        <span
                          class="msg-time-group"
                          :title="formatDeletedDate(item)"
                        >
                          <span class="msg-time">{{
                            formatTime(item.createdUtc)
                          }}</span>
                          <svg
                            class="msg-deleted-icon"
                            viewBox="-2.27 -3.0 28.54 28.54"
                            fill="none"
                          >
                            <path
                              d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"
                              stroke="currentColor"
                              stroke-width="2"
                              stroke-linecap="round"
                            />
                          </svg>
                        </span>
                        <button
                          class="msg-eye-btn"
                          title="Скрыть"
                          @click="toggleDeletedExpand(item.id)"
                        >
                          <svg
                            class="eye-open"
                            viewBox="0.3 0.57 23.35 23.35"
                            width="14"
                            height="14"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="2"
                            stroke-linecap="round"
                            stroke-linejoin="round"
                          >
                            <path
                              d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"
                            />
                            <circle cx="12" cy="12" r="3" />
                          </svg>
                          <svg
                            class="eye-closed"
                            viewBox="0.3 0.57 23.35 23.35"
                            width="14"
                            height="14"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="2"
                            stroke-linecap="round"
                          >
                            <path d="M3 12c0 0 4 5 9 5s9-5 9-5" />
                          </svg>
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
                  <div
                    v-if="item.isContinuation"
                    class="msg-layout msg-continuation"
                  >
                    <div class="msg-time-gutter">
                      <span class="msg-time-hover" :title="formatFullDate(item)"
                        >{{ formatTime(item.createdUtc)
                        }}<svg
                          v-if="item.modifiedUtc"
                          class="msg-edited-icon"
                          viewBox="-0.7 -1.2 25.4 25.4"
                          fill="none"
                        >
                          <path
                            d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"
                            stroke="currentColor"
                            stroke-width="2"
                            stroke-linecap="round"
                            stroke-linejoin="round"
                          /></svg
                      ></span>
                    </div>
                    <div class="msg-body">
                      <template v-if="!isEditing(item.id)">
                        <div
                          :ref="
                            (el) =>
                              checkMessageHeight(item.id, el as HTMLElement)
                          "
                          class="msg-content"
                          :class="{ collapsed: isTruncated(item) }"
                        >
                          <div class="msg-text" v-html="item.text" />
                        </div>
                        <div
                          v-if="needsTruncation(item)"
                          class="msg-expand-row msg-expand-row-compact"
                        >
                          <button
                            class="expand-toggle"
                            @click="toggleExpand(item.id)"
                          >
                            <svg
                              v-if="isExpanded(item.id)"
                              viewBox="0 0 12 12"
                              width="16"
                              height="16"
                              fill="none"
                              stroke="currentColor"
                              stroke-width="1.5"
                              stroke-linecap="round"
                              stroke-linejoin="round"
                            >
                              <path d="M2 8L6 4L10 8" />
                            </svg>
                            <svg
                              v-else
                              viewBox="0 0 12 12"
                              width="16"
                              height="16"
                              fill="none"
                              stroke="currentColor"
                              stroke-width="1.5"
                              stroke-linecap="round"
                              stroke-linejoin="round"
                            >
                              <path d="M2 4L6 8L10 4" />
                            </svg>
                          </button>
                        </div>
                        <div
                          v-if="item.likes?.length > 0"
                          class="msg-reactions"
                        >
                          <button
                            class="reaction-badge"
                            :class="{ 'my-reaction': isLikedByMe(item) }"
                            :title="getLikesTooltip(item)"
                            @click="toggleLike(item)"
                          >
                            <svg
                              viewBox="-1.2 -0.75 26.4 26.4"
                              width="14"
                              height="14"
                              class="reaction-heart"
                              fill="none"
                            >
                              <path
                                d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"
                                stroke="currentColor"
                                stroke-width="2"
                              />
                            </svg>
                            <span class="reaction-count">{{
                              item.likes.length
                            }}</span>
                          </button>
                        </div>
                      </template>
                      <div v-else class="msg-edit">
                        <BBCodeEditor
                          ref="editEditorRef"
                          v-model="editText"
                          context="message"
                          placeholder="Редактирование сообщения..."
                          :min-height="60"
                          :max-height="200"
                          @submit="handleEditSubmit"
                        />
                        <div class="msg-edit-actions">
                          <button
                            type="button"
                            class="msg-edit-btn"
                            @click="cancelEdit"
                          >
                            Отмена
                          </button>
                          <button
                            type="button"
                            class="msg-edit-btn"
                            @click="saveEdit(item.id)"
                          >
                            Сохранить
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>

                  <!-- Full message (with avatar and header) -->
                  <div v-else class="msg-layout">
                    <router-link
                      :to="{
                        name: 'profile',
                        params: { login: item.author.login },
                      }"
                      class="msg-avatar-link"
                    >
                      <img
                        :src="item.author.smallPictureUrl || defaultAvatar"
                        :alt="item.author.login"
                        class="msg-avatar"
                      />
                    </router-link>

                    <div class="msg-body">
                      <div class="msg-header">
                        <router-link
                          :to="{
                            name: 'profile',
                            params: { login: item.author.login },
                          }"
                          class="msg-author"
                          :class="{ online: isOnline(item.author) }"
                          >{{ item.author.login }}</router-link
                        >
                        <span
                          class="msg-time-group"
                          :title="formatFullDate(item)"
                        >
                          <span class="msg-time">{{
                            formatTime(item.createdUtc)
                          }}</span
                          ><svg
                            v-if="item.modifiedUtc"
                            class="msg-edited-icon"
                            viewBox="-0.7 -1.2 25.4 25.4"
                            fill="none"
                          >
                            <path
                              d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"
                              stroke="currentColor"
                              stroke-width="2"
                              stroke-linecap="round"
                              stroke-linejoin="round"
                            />
                          </svg>
                        </span>
                        <a
                          class="msg-anchor-btn"
                          title="Ссылка на сообщение"
                          :href="`#msg-${item.id}`"
                          @click.prevent="copyAnchor(item.id)"
                        >
                          <svg
                            viewBox="-2 -2 28 28"
                            width="12"
                            height="12"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="2"
                            stroke-linecap="round"
                            stroke-linejoin="round"
                          >
                            <path
                              d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"
                            />
                            <path
                              d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"
                            />
                          </svg>
                        </a>
                      </div>

                      <template v-if="!isEditing(item.id)">
                        <div
                          :ref="
                            (el) =>
                              checkMessageHeight(item.id, el as HTMLElement)
                          "
                          class="msg-content"
                          :class="{ collapsed: isTruncated(item) }"
                        >
                          <div class="msg-text" v-html="item.text" />
                        </div>
                        <div
                          v-if="needsTruncation(item)"
                          class="msg-expand-row"
                        >
                          <button
                            class="expand-toggle"
                            @click="toggleExpand(item.id)"
                          >
                            <svg
                              v-if="isExpanded(item.id)"
                              viewBox="0 0 12 12"
                              width="16"
                              height="16"
                              fill="none"
                              stroke="currentColor"
                              stroke-width="1.5"
                              stroke-linecap="round"
                              stroke-linejoin="round"
                            >
                              <path d="M2 8L6 4L10 8" />
                            </svg>
                            <svg
                              v-else
                              viewBox="0 0 12 12"
                              width="16"
                              height="16"
                              fill="none"
                              stroke="currentColor"
                              stroke-width="1.5"
                              stroke-linecap="round"
                              stroke-linejoin="round"
                            >
                              <path d="M2 4L6 8L10 4" />
                            </svg>
                          </button>
                        </div>
                        <div
                          v-if="item.likes?.length > 0"
                          class="msg-reactions"
                        >
                          <button
                            class="reaction-badge"
                            :class="{ 'my-reaction': isLikedByMe(item) }"
                            :title="getLikesTooltip(item)"
                            @click="toggleLike(item)"
                          >
                            <svg
                              viewBox="-1.2 -0.75 26.4 26.4"
                              width="14"
                              height="14"
                              class="reaction-heart"
                              fill="none"
                            >
                              <path
                                d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"
                                stroke="currentColor"
                                stroke-width="2"
                              />
                            </svg>
                            <span class="reaction-count">{{
                              item.likes.length
                            }}</span>
                          </button>
                        </div>
                      </template>

                      <div v-else class="msg-edit">
                        <BBCodeEditor
                          ref="editEditorRef"
                          v-model="editText"
                          context="message"
                          placeholder="Редактирование сообщения..."
                          :min-height="60"
                          :max-height="200"
                          @submit="handleEditSubmit"
                        />
                        <div class="msg-edit-actions">
                          <button
                            type="button"
                            class="msg-edit-btn"
                            @click="cancelEdit"
                          >
                            Отмена
                          </button>
                          <button
                            type="button"
                            class="msg-edit-btn"
                            @click="saveEdit(item.id)"
                          >
                            Сохранить
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                </template>
              </div>
            </template>
          </template>
        </div>

        <!-- Scroll to latest button (centered over messages) -->
        <button
          v-if="hasMoreAfter"
          class="scroll-to-latest"
          @click="jumpToLatest"
        >
          <svg
            viewBox="0 0 24 24"
            width="24"
            height="24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <path d="M7 13l5 5 5-5M7 6l5 5 5-5" />
          </svg>
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
              :draft-key="`conversation_${selectedConversation?.id}`"
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
            <button
              class="toolbar-btn toolbar-btn-delete-confirm"
              title="Подтвердить удаление"
              @click="confirmDelete"
            >
              <svg
                viewBox="-2.27 -3.0 28.54 28.54"
                width="20"
                height="20"
                fill="none"
              >
                <path
                  d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                />
              </svg>
            </button>
            <button
              class="toolbar-btn toolbar-btn-cancel"
              title="Отмена"
              @click="cancelDelete"
            >
              <svg
                viewBox="3.1 3.1 17.8 17.8"
                width="20"
                height="20"
                fill="none"
              >
                <path
                  d="M18 6L6 18M6 6l12 12"
                  stroke="currentColor"
                  stroke-width="1.48"
                  stroke-linecap="round"
                />
              </svg>
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
              <svg
                viewBox="-1.2 -0.75 26.4 26.4"
                width="20"
                height="20"
                fill="none"
              >
                <path
                  d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"
                  stroke="currentColor"
                  stroke-width="2"
                />
              </svg>
            </button>
            <button
              v-if="canEditMessage(hoveredMessage)"
              class="toolbar-btn"
              title="Редактировать"
              @click="startEdit(hoveredMessage)"
            >
              <svg
                viewBox="-0.7 -1.2 25.4 25.4"
                width="20"
                height="20"
                fill="none"
              >
                <path
                  d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                />
              </svg>
            </button>
            <button
              v-if="canDeleteMessage(hoveredMessage)"
              class="toolbar-btn"
              title="Удалить"
              @click="requestDelete(hoveredMessage.id)"
            >
              <svg
                viewBox="-2.27 -3.0 28.54 28.54"
                width="20"
                height="20"
                fill="none"
              >
                <path
                  d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                />
              </svg>
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
@import "src/assets/styles/BbcodeContent"
@import "src/assets/styles/Inputs"

.conversation-view
  display: flex
  flex-direction: column
  min-height: 400px

.conversation-header
  display: flex
  align-items: center
  gap: $medium
  padding: $small $medium
  border: 1px dashed $border
  margin-bottom: $medium

.back-button
  +button()
  padding: $tiny $small

.interlocutor
  display: flex
  align-items: center
  gap: $small
  text-decoration: none

.header-avatar
  width: 40px
  height: 40px
  border-radius: 50%
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
  &::after
    content: ""
    display: block
    height: $medium
  position: relative
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
  align-self: flex-start

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
  color: $border

.msg-author
  text-decoration: none
  font-weight: 500
  color: $text-muted
  &:hover
    color: $link-nav-hover
    text-decoration: underline
  &.online
    color: $accent-green
    &:hover
      color: $accent-green-hover
      text-decoration: underline

.msg-time-group
  display: inline-flex
  align-items: baseline
  gap: 6px
  color: $text-muted

.msg-edited-icon
  width: 14px
  height: 14px
  margin-left: -3px
  flex-shrink: 0
  position: relative
  top: 2px

.msg-header-btn,
.msg-eye-btn,
.msg-anchor-btn
  display: inline-flex
  align-items: center
  justify-content: center
  padding: 0
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted
  svg
    flex-shrink: 0
  &:hover
    filter: brightness($hover-brightness)

.msg-anchor-btn
  align-self: flex-end
  margin-bottom: 4px

.msg-eye-btn
  position: relative
  top: 2px
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
  color: $text
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
  :deep()
    +bbcode-content
    // NSFW styles now in _BbcodeContent.sass

.msg-edit
  margin-top: 0

.msg-edit-actions
  display: flex
  justify-content: flex-start
  gap: $small
  margin-top: $small

.msg-edit-btn
  +button
  font-size: $secondary-font-size

.msg-expand-row
  display: flex
  justify-content: center
  margin-top: $small
  margin-left: calc(-40px - #{$small})
  width: calc(100% + 40px + #{$small})

.expand-toggle
  display: inline-flex
  align-items: center
  justify-content: center
  padding: 2px 6px
  border: none
  background: transparent
  color: $text-muted
  cursor: pointer
  &:hover
    filter: brightness($hover-brightness)

.msg-reactions
  display: flex
  align-items: center
  gap: $tiny
  margin-top: $small

.msg-expand-row + .msg-reactions
  margin-top: -8px

:global(.msg-toolbar-fixed)
  position: fixed
  display: flex
  align-items: center
  gap: 0
  padding: 0
  background-color: $bg-element
  box-shadow: 0 0 0 1px var(--hover-overlay), 0 2px 8px var(--shadow-color)
  border-radius: $border-radius
  z-index: 10000

:global(.toolbar-btn-delete-confirm)
  color: var(--accent-red) !important
  &:hover
    color: var(--accent-red-hover) !important

:global(.toolbar-btn-cancel)
  color: var(--text-muted) !important
  &:hover
    color: var(--heading-alt) !important

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
  color: $text-muted
  transition: background-color 0.1s ease, color 0.1s ease

:global(.toolbar-btn svg)
  transition: transform 0.15s ease

:global(.toolbar-btn:hover)
  background-color: $bg-element-accent
  color: $text

:global(.toolbar-btn:hover svg)
  transform: scale(1.15)

:global(.toolbar-btn:active)
  background-color: var(--hover-overlay)

:global(.toolbar-btn:active svg)
  transform: scale(0.9)

:global(.toolbar-btn.active)
  color: $text-muted

:global(.toolbar-btn.active svg)
  fill: currentColor

:global(.toolbar-btn.active:hover)
  color: $text
  background-color: $bg-element-accent

.reaction-badge
  display: inline-flex
  align-items: center
  gap: 6px
  padding: 6px 10px
  border: none
  background-color: $hover-overlay
  color: $text-muted
  cursor: pointer
  transition: background-color 0.1s ease, color 0.1s ease, transform 0.1s ease
  border-radius: $border-radius
  svg
    width: 18px
    height: 18px
    fill: none
    transition: transform 0.15s ease
  &:hover
    background-color: $active-overlay
    color: $text
    svg
      transform: scale(1.15)
  &:active
    background-color: $hover-overlay
    transform: scale(0.95)
  &.my-reaction
    svg
      fill: currentColor

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
  transition: all 0.15s ease
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
  +button()
  align-self: flex-start

.banned-hint
  flex: 1
  text-align: center
  padding: $small
  color: $accent-red
</style>
