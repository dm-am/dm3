<script setup lang="ts">
import { onMounted, onUnmounted, ref, nextTick, computed, watch } from "vue";
import { storeToRefs } from "pinia";
import { useChatStore } from "@/stores/chat";
import { useUserStore } from "@/stores";
import { AccessPolicy, UserRole } from "@/api/models/community";
import type { ChatMessage } from "@/api/models/chat";
import dayjs from "dayjs";
import defaultAvatar from "@/assets/images/userpic.png";
import BBCodeEditor from "@/components/inputs/BBCodeEditor.vue";
import chatApi from "@/api/requests/chatApi";
import { initBbcodeInteractive } from "@/utils/bbcodeInteractive";

const chatStore = useChatStore();
const userStore = useUserStore();
const {
  messages,
  loading,
  loadingBefore,
  loadingAfter,
  sending,
  hasMoreBefore,
  hasMoreAfter,
  highlightedMessageId,
} = storeToRefs(chatStore);
const { user } = storeToRefs(userStore);

const ONLINE_THRESHOLD_MINUTES = 15;
const EDIT_TIME_LIMIT_MINUTES = 15;
const MAX_MESSAGE_HEIGHT = 500;

// Compact mode (persisted in localStorage)
const COMPACT_MODE_KEY = "chat-compact-mode";
const isCompactMode = ref(localStorage.getItem(COMPACT_MODE_KEY) === "true");
function toggleCompactMode() {
  const container = messagesContainer.value;
  let wasAtBottom = false;
  let bottomMessageId: string | null = null;

  if (container) {
    // Check if we're at the bottom (within 50px threshold)
    wasAtBottom =
      container.scrollHeight - container.scrollTop - container.clientHeight <
      50;

    if (!wasAtBottom) {
      // Find the bottom-most visible message
      const containerRect = container.getBoundingClientRect();
      const messageElements = container.querySelectorAll(
        ".chat-message[data-id]",
      );
      for (const el of messageElements) {
        const rect = el.getBoundingClientRect();
        if (rect.top < containerRect.bottom) {
          bottomMessageId = el.getAttribute("data-id");
        }
      }
    }
  }

  isCompactMode.value = !isCompactMode.value;
  localStorage.setItem(COMPACT_MODE_KEY, String(isCompactMode.value));

  nextTick(() => {
    if (wasAtBottom) {
      // Stay at bottom
      scrollToBottom();
    } else if (bottomMessageId && messagesContainer.value) {
      // Keep the same message at bottom of viewport
      const el = messagesContainer.value.querySelector(
        `.chat-message[data-id="${bottomMessageId}"]`,
      );
      if (el) {
        const containerRect = messagesContainer.value.getBoundingClientRect();
        const elRect = el.getBoundingClientRect();
        messagesContainer.value.scrollTop +=
          elRect.bottom - containerRect.bottom;
      }
    }
  });
}

const isBanned = computed(() => {
  if (!user.value?.accessPolicy) return false;
  const policy = user.value.accessPolicy;
  return (
    policy === AccessPolicy.DemocraticBan || policy === AccessPolicy.FullBan
  );
});

const isModerator = computed(() => {
  if (!user.value) return false;
  return (
    user.value.roles?.some((r: UserRole) =>
      [UserRole.Admin, UserRole.SeniorModerator, UserRole.Moderator].includes(
        r,
      ),
    ) ?? false
  );
});

const canSendMessages = computed(() => user.value && !isBanned.value);

// Typing indicator (placeholder - will be connected to WebSocket later)
const typingUsers = ref<string[]>([]);
const typingText = computed(() => {
  if (typingUsers.value.length === 0) return "";
  if (typingUsers.value.length === 1)
    return `${typingUsers.value[0]} печатает...`;
  if (typingUsers.value.length === 2)
    return `${typingUsers.value[0]} и ${typingUsers.value[1]} печатают...`;
  return `${typingUsers.value[0]} и ещё ${typingUsers.value.length - 1} печатают...`;
});

const newMessage = ref("");
const chatContainer = ref<HTMLElement | null>(null);
const messagesContainer = ref<HTMLElement | null>(null);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const editEditorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const topSentinel = ref<HTMLElement | null>(null);
const bottomSentinel = ref<HTMLElement | null>(null);
const viewToggleRef = ref<HTMLElement | null>(null);
const viewIndicatorStyle = ref({ left: "0px", width: "50%" });

function updateViewIndicator() {
  if (!viewToggleRef.value) return;
  const btns = viewToggleRef.value.querySelectorAll(".view-toggle-btn");
  const activeIndex = isCompactMode.value ? 0 : 1;
  const activeBtn = btns[activeIndex] as HTMLElement;
  if (!activeBtn) return;
  viewIndicatorStyle.value = {
    left: `${activeBtn.offsetLeft}px`,
    width: `${activeBtn.offsetWidth}px`,
  };
}

watch(isCompactMode, () => {
  nextTick(updateViewIndicator);
});

let topObserver: IntersectionObserver | null = null;
let bottomObserver: IntersectionObserver | null = null;

// Scroll position tracking
let isLoadingOlder = false;
let isLoadingNewer = false;
const isScrolling = ref(false);
let scrollEndTimeout: ReturnType<typeof setTimeout> | null = null;
let isInitialScrolling = false; // Block infinite scroll during initial anchor navigation

function setupInfiniteScroll() {
  if (!messagesContainer.value) return;

  // Top sentinel - load older messages
  if (topSentinel.value) {
    topObserver = new IntersectionObserver(
      async (entries) => {
        if (
          isInitialScrolling ||
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
        await chatStore.fetchMoreBefore();

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

  // Bottom sentinel - load newer messages
  if (bottomSentinel.value) {
    bottomObserver = new IntersectionObserver(
      async (entries) => {
        if (isInitialScrolling || !entries[0].isIntersecting || isLoadingNewer || !hasMoreAfter.value)
          return;
        isLoadingNewer = true;

        await chatStore.fetchMoreAfter();
        isLoadingNewer = false;
      },
      {
        root: messagesContainer.value,
        rootMargin: "0px 0px 100px 0px",
        threshold: 0,
      },
    );
    bottomObserver.observe(bottomSentinel.value);
  }
}

function cleanupInfiniteScroll() {
  topObserver?.disconnect();
  bottomObserver?.disconnect();
  topObserver = null;
  bottomObserver = null;
}

// autoGrowEdit removed - BBCodeEditor handles its own sizing

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
  return messages.value.find((m) => m.id === hoveredMessageId.value) || null;
});

// Toolbar is always visible when message is hovered - clipping is handled by CSS
const isToolbarVisible = computed(() => {
  return !!(hoveredMessage.value && chatContainer.value);
});

function handleMessageMouseEnter(event: MouseEvent, msgId: string) {
  if (isScrolling.value) return;
  if (hideToolbarTimeout) {
    clearTimeout(hideToolbarTimeout);
    hideToolbarTimeout = null;
  }

  const target = event.currentTarget as HTMLElement;
  const rect = target.getBoundingClientRect();
  const container = chatContainer.value;

  if (!container) return;

  const containerRect = container.getBoundingClientRect();

  // Calculate position relative to chat-container
  toolbarPosition.value = {
    top: rect.top - containerRect.top - 16,
    right: containerRect.right - rect.right + 8,
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
  isScrolling.value = true;
  if (hoveredMessageId.value) {
    hoveredMessageId.value = null;
    confirmingDeleteId.value = null;
    isToolbarHovered.value = false;
  }
  if (scrollEndTimeout) {
    clearTimeout(scrollEndTimeout);
  }
  scrollEndTimeout = setTimeout(() => {
    isScrolling.value = false;
    scrollEndTimeout = null;
  }, 150);
}

function handleScroll() {
  isScrolling.value = true;
  if (hoveredMessageId.value) {
    hoveredMessageId.value = null;
    confirmingDeleteId.value = null;
    isToolbarHovered.value = false;
  }
  if (scrollEndTimeout) {
    clearTimeout(scrollEndTimeout);
  }
  scrollEndTimeout = setTimeout(() => {
    isScrolling.value = false;
    scrollEndTimeout = null;
  }, 150);
}

// Expanded messages
const expandedMessages = ref<Set<string>>(new Set());
const truncatedMessages = ref<Set<string>>(new Set());
const expandedDeletedMessages = ref<Set<string>>(new Set());

// Group messages with date separators
type MessageOrSeparator =
  | (ChatMessage & { isContinuation?: boolean })
  | { type: "date-separator"; date: string; formattedDate: string };

const CONTINUATION_TIME_LIMIT_MINUTES = 5;

const messagesWithSeparators = computed((): MessageOrSeparator[] => {
  if (!messages.value?.length) return [];

  const result: MessageOrSeparator[] = [];
  let lastDate: string | null = null;
  let lastAuthor: string | null = null;
  let lastMessageTime: dayjs.Dayjs | null = null;

  for (const msg of messages.value) {
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

// Generate recent dates for archive links
const recentDates = computed(() => {
  const dates = [];
  for (let i = 0; i < 10; i++) {
    const date = dayjs().subtract(i, "day");
    dates.push({
      value: date.format("YYYY-MM-DD"),
      label: date.format("DD.MM.YYYY"),
    });
  }
  return dates;
});

// Replace images with links in compact mode
function replaceImagesWithLinks(container: HTMLElement | null) {
  if (!container || !isCompactMode.value) return;

  const images = container.querySelectorAll(".msg-text img");
  images.forEach((img) => {
    const imgEl = img as HTMLImageElement;
    if (imgEl.dataset.replaced) return; // Already replaced

    const link = document.createElement("a");
    link.href = imgEl.src;
    link.target = "_blank";
    link.className = "compact-image-link";
    // Use alt text from data-alt attribute if available, otherwise default
    const altText = imgEl.dataset.alt || imgEl.alt;
    link.textContent = altText ? `[${altText}]` : "[изображение]";
    link.title = imgEl.src;

    imgEl.style.display = "none";
    imgEl.dataset.replaced = "true";
    imgEl.parentNode?.insertBefore(link, imgEl);
  });
}

// Restore images when leaving compact mode
function restoreImages(container: HTMLElement | null) {
  if (!container) return;

  const links = container.querySelectorAll(".compact-image-link");
  links.forEach((link) => link.remove());

  const images = container.querySelectorAll(".msg-text img[data-replaced]");
  images.forEach((img) => {
    const imgEl = img as HTMLImageElement;
    imgEl.style.display = "";
    delete imgEl.dataset.replaced;
  });
}

// Watch compact mode changes
watch(isCompactMode, (newValue) => {
  nextTick(() => {
    if (newValue) {
      replaceImagesWithLinks(messagesContainer.value);
    } else {
      restoreImages(messagesContainer.value);
    }
  });
});

// Watch for new messages to init interactive BBCode elements
watch(
  messages,
  () => {
    nextTick(() => {
      initBbcodeInteractive(messagesContainer.value);
      if (isCompactMode.value) {
        replaceImagesWithLinks(messagesContainer.value);
      }
    });
  },
  { deep: true },
);

onMounted(async () => {
  const hashMsgId = getHashMessageId();

  if (hashMsgId) {
    isInitialScrolling = true;
    await chatStore.navigateToMessage(hashMsgId);
    nextTick(() => {
      scrollToMessage(hashMsgId);
      setupInfiniteScroll();
      initBbcodeInteractive(messagesContainer.value);
      // Allow infinite scroll after animation completes
      setTimeout(() => {
        isInitialScrolling = false;
      }, 600);
    });
  } else {
    await chatStore.fetchMessages();
    scrollToBottom();
    nextTick(() => {
      setupInfiniteScroll();
      initBbcodeInteractive(messagesContainer.value);
    });
  }

  // Apply image replacement if in compact mode
  if (isCompactMode.value) {
    replaceImagesWithLinks(messagesContainer.value);
  }

  // Initialize view toggle indicator
  nextTick(updateViewIndicator);
});

onUnmounted(() => {
  cleanupInfiniteScroll();
  if (hideToolbarTimeout) clearTimeout(hideToolbarTimeout);
  if (scrollEndTimeout) clearTimeout(scrollEndTimeout);
});

// Watch for highlighted message changes
watch(highlightedMessageId, (newId) => {
  if (newId) {
    nextTick(() => {
      scrollToMessage(newId);
    });
  }
});

// Formatting
function formatTime(dateStr: string) {
  return dayjs(dateStr).format("HH:mm");
}

function formatFullDate(msg: ChatMessage) {
  let result = `Отправлено: ${dayjs(msg.createdUtc).format("DD.MM.YYYY HH:mm")}`;
  if (msg.edits?.length) {
    for (const edit of msg.edits) {
      const isSelf = user.value && edit.editor?.login === user.value.login;
      const editorName = isSelf ? "вами" : edit.editor?.login || "неизвестно";
      result += `\nРедактирование: ${dayjs(edit.editedAtUtc).format("DD.MM.YYYY HH:mm")} (${editorName})`;
    }
  }
  return result;
}

function formatDeletedDate(msg: ChatMessage) {
  let result = `Отправлено: ${dayjs(msg.createdUtc).format("DD.MM.YYYY HH:mm")}`;
  if (msg.edits?.length) {
    for (const edit of msg.edits) {
      const isSelf = user.value && edit.editor?.login === user.value.login;
      const editorName = isSelf ? "вами" : edit.editor?.login || "неизвестно";
      result += `\nРедактирование: ${dayjs(edit.editedAtUtc).format("DD.MM.YYYY HH:mm")} (${editorName})`;
    }
  }
  const deleterLogin = msg.deletedBy?.login;
  const isSelfDelete = user.value && deleterLogin === user.value.login;
  const deleterName = isSelfDelete ? "вами" : deleterLogin || "неизвестно";
  const deletedAtUtcStr = msg.deletedAtUtc
    ? dayjs(msg.deletedAtUtc).format("DD.MM.YYYY HH:mm")
    : "";
  result += deletedAtUtcStr
    ? `\nУдалено: ${deletedAtUtcStr} (${deleterName})`
    : `\nУдалено (${deleterName})`;
  return result;
}

// Track latest online status per login
const latestOnlineByLogin = computed(() => {
  const map = new Map<string, string>();
  if (!messages.value?.length) return map;
  for (const msg of messages.value) {
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
function canEditMessage(msg: ChatMessage) {
  if (!user.value || msg.isRemoved) return false;
  if (isModerator.value) return true;
  if (msg.author?.login !== user.value.login) return false;
  const minutesSinceCreation = dayjs().diff(
    dayjs(msg.createdUtc),
    "minute",
    true,
  );
  return minutesSinceCreation <= EDIT_TIME_LIMIT_MINUTES;
}

function canDeleteMessage(msg: ChatMessage) {
  return canEditMessage(msg);
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
function canLikeMessage(_msg: ChatMessage) {
  return !!user.value;
}

function isLikedByMe(msg: ChatMessage) {
  if (!user.value) return false;
  return msg.likes?.some((u: any) => u.login === user.value?.login) ?? false;
}

function getLikesTooltip(msg: ChatMessage) {
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

async function startEdit(msg: ChatMessage) {
  editingId.value = msg.id;
  // Fetch the original BBCode from the backend
  const { data } = await chatApi.getMessageForEdit(msg.id);
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
  // Reinitialize interactive elements after DOM update
  nextTick(() => {
    initBbcodeInteractive(messagesContainer.value);
    if (isCompactMode.value) {
      replaceImagesWithLinks(messagesContainer.value);
    }
  });
}

async function saveEdit(msgId: string) {
  if (editText.value.trim()) {
    await chatStore.updateMessage(msgId, editText.value);
  }
  cancelEdit();
}

function handleEditSubmit() {
  if (editingId.value) {
    saveEdit(editingId.value);
  }
}

// Message truncation (long messages)
function needsTruncation(msg: ChatMessage) {
  // Check for [cut] marker in HTML (rendered as bb-cut-marker class)
  return (
    msg.text?.includes("bb-cut-marker") || truncatedMessages.value.has(msg.id)
  );
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

function isTruncated(msg: ChatMessage) {
  return needsTruncation(msg) && !isExpanded(msg.id);
}

function toggleExpand(msgId: string) {
  if (expandedMessages.value.has(msgId)) {
    expandedMessages.value.delete(msgId);
  } else {
    expandedMessages.value.add(msgId);
  }
  if (hoveredMessageId.value === msgId && chatContainer.value) {
    nextTick(() => {
      const msgElement = document.getElementById(`msg-${msgId}`);
      const container = chatContainer.value;
      if (msgElement && container) {
        const rect = msgElement.getBoundingClientRect();
        const containerRect = container.getBoundingClientRect();
        toolbarPosition.value = {
          top: rect.top - containerRect.top - 16,
          right: containerRect.right - rect.right + 8,
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
async function toggleLike(msg: ChatMessage) {
  if (!user.value) return;
  if (isLikedByMe(msg)) {
    await chatStore.unlikeMessage(msg.id);
  } else {
    await chatStore.likeMessage(msg.id);
  }
}

// Anchor
function copyAnchor(msgId: string) {
  const url = `${window.location.origin}${window.location.pathname}#msg-${msgId}`;
  navigator.clipboard.writeText(url);
}

function scrollToMessage(msgId: string) {
  nextTick(() => {
    const element = document.getElementById(`msg-${msgId}`);
    if (element) {
      element.scrollIntoView({ behavior: "smooth", block: "center" });
      // Delay highlight until after scroll animation (~500ms)
      setTimeout(() => {
        element.classList.add("highlighted");
        setTimeout(() => {
          element.classList.remove("highlighted");
          chatStore.clearHighlight();
        }, 1500);
      }, 400);
    }
  });
}

function getHashMessageId(): string | null {
  const hash = window.location.hash;
  if (hash && hash.startsWith("#msg-")) {
    return hash.slice(5);
  }
  return null;
}

// Archive date navigation
async function loadLogsForDate(date: string) {
  await chatStore.navigateToDate(date);
}

// Jump to latest
async function jumpToLatest() {
  await chatStore.jumpToLatest();
  scrollToBottom();
}

function scrollToBottom() {
  nextTick(() => {
    if (messagesContainer.value) {
      messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight;
    }
  });
}

async function handleSend() {
  if (!newMessage.value.trim() || sending.value) return;
  const text = newMessage.value;
  newMessage.value = "";
  editorRef.value?.clear();
  await chatStore.sendMessage(text);
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
    await chatStore.deleteMessage(confirmingDeleteId.value);
    confirmingDeleteId.value = null;
  }
}
</script>

<template>
  <page-title>Глобальный чат</page-title>

  <!-- Archive links -->
  <div class="chat-archive">
    <div class="archive-dates">
      <span class="archive-label">Архив: </span>
      <template v-for="(date, index) in recentDates" :key="date.value">
        <a
          class="archive-link"
          :href="`/chat?date=${date.value}`"
          @click.prevent="loadLogsForDate(date.value)"
          >{{ date.label }}</a
        ><span v-if="index < recentDates.length - 1" class="archive-sep"
          >,
        </span>
      </template>
    </div>
    <div ref="viewToggleRef" class="view-toggle">
      <button
        class="view-toggle-btn"
        :class="{ active: isCompactMode }"
        title="Компактный вид"
        @click="!isCompactMode && toggleCompactMode()"
      >
        <svg viewBox="0 0 16 12" width="16" height="12" fill="currentColor">
          <rect x="0" y="0" width="16" height="2" />
          <rect x="0" y="3.33" width="16" height="2" />
          <rect x="0" y="6.67" width="16" height="2" />
          <rect x="0" y="10" width="16" height="2" />
        </svg>
      </button>
      <button
        class="view-toggle-btn"
        :class="{ active: !isCompactMode }"
        title="Обычный вид"
        @click="isCompactMode && toggleCompactMode()"
      >
        <svg viewBox="0 0 16 12" width="16" height="12" fill="currentColor">
          <circle cx="1.5" cy="1.5" r="1.5" />
          <rect x="5" y="0" width="11" height="2.5" />
          <circle cx="1.5" cy="6" r="1.5" />
          <rect x="5" y="4.75" width="11" height="2.5" />
          <circle cx="1.5" cy="10.5" r="1.5" />
          <rect x="5" y="9.25" width="11" height="2.5" />
        </svg>
      </button>
      <span class="view-indicator" :style="viewIndicatorStyle"></span>
    </div>
  </div>

  <div ref="chatContainer" class="chat-container" :class="{ 'compact-mode': isCompactMode }">
    <div
      ref="messagesContainer"
      class="chat-messages"
      :class="{ 'is-scrolling': isScrolling }"
      @scroll="handleScroll"
      @wheel.passive="handleWheel"
    >
      <the-loader v-if="loading" :big="true" />
      <secondary-text v-else-if="!messages?.length" class="chat-empty">
        Сообщений пока нет. Начните общение!
      </secondary-text>
      <template v-else>
        <!-- Top sentinel for loading older messages -->
        <div
          v-if="hasMoreBefore"
          ref="topSentinel"
          class="scroll-sentinel top-sentinel"
        >
          <the-loader v-if="loadingBefore" :small="true" />
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
            :data-id="item.id"
            class="chat-message"
            :class="{
              removed: item.isRemoved,
              hovered: hoveredMessageId === item.id,
              continuation: item.isContinuation,
              'deleted-collapsed': item.isRemoved && !isDeletedExpanded(item.id),
            }"
            @mouseenter="handleMessageMouseEnter($event, item.id)"
            @mouseleave="handleMessageMouseLeave"
          >
            <!-- Deleted message -->
            <template v-if="item.isRemoved">
              <!-- Compact mode: time + "Сообщение удалено" (clickable for moderators) -->
              <div
                v-if="isCompactMode && !isDeletedExpanded(item.id)"
                class="msg-layout msg-layout-compact"
              >
                <div class="msg-body">
                  <div class="msg-header msg-header-compact">
                    <span
                      class="msg-time-group"
                      :title="formatDeletedDate(item)"
                    >
                      <span class="msg-icon-placeholder"></span
                      ><span class="msg-time">{{
                        formatTime(item.createdUtc)
                      }}</span>
                    </span>
                    <span
                      class="msg-deleted-inline"
                      :class="{ clickable: isModerator }"
                      @click="isModerator && toggleDeletedExpand(item.id)"
                      >Сообщение удалено</span
                    >
                  </div>
                </div>
              </div>
              <!-- Normal mode: circle + text -->
              <div v-else-if="!isDeletedExpanded(item.id)" class="msg-layout">
                <div class="msg-avatar-placeholder">
                  <svg
                    viewBox="0 0 64 64"
                    width="64"
                    height="64"
                    class="deleted-avatar"
                  >
                    <circle
                      cx="32"
                      cy="32"
                      r="30"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="1"
                      stroke-dasharray="4 2"
                    />
                    <path
                      d="M20 20l24 24M44 20l-24 24"
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
              <div
                v-else
                class="msg-layout"
                :class="{ 'msg-layout-compact': isCompactMode }"
              >
                <router-link
                  v-if="!isCompactMode"
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
                  <div
                    class="msg-header"
                    :class="{ 'msg-header-compact': isCompactMode }"
                  >
                    <!-- Compact mode: trash + time | name + Скрыть -->
                    <template v-if="isCompactMode">
                      <span
                        class="msg-time-group"
                        :title="formatDeletedDate(item)"
                      >
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
                        </svg
                        ><span class="msg-time">{{
                          formatTime(item.createdUtc)
                        }}</span>
                      </span>
                      <router-link
                        :to="{
                          name: 'profile',
                          params: { login: item.author.login },
                        }"
                        class="msg-author"
                        :class="{ online: isOnline(item.author) }"
                        >{{ item.author.login }}</router-link
                      ><a
                        class="msg-hide-link msg-hide-link-inline"
                        href="#"
                        @click.prevent="toggleDeletedExpand(item.id)"
                        >(скрыть)</a
                      >
                    </template>
                    <!-- Normal mode: name, time, "Скрыть" link -->
                    <template v-else>
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
                      <a
                        class="msg-hide-link"
                        href="#"
                        @click.prevent="toggleDeletedExpand(item.id)"
                        >(скрыть)</a
                      >
                    </template>
                  </div>
                  <div class="msg-content">
                    <div class="msg-text" v-html="item.text" />
                  </div>
                </div>
              </div>
            </template>

            <!-- Normal message -->
            <template v-else>
              <!-- Continuation message (only in normal mode, not compact) -->
              <div
                v-if="item.isContinuation && !isCompactMode"
                class="msg-layout msg-continuation"
              >
                <div class="msg-time-gutter">
                  <span
                    class="msg-time-group msg-time-hover"
                    :title="formatFullDate(item)"
                    ><span class="msg-time">{{
                      formatTime(item.createdUtc)
                    }}</span
                    ><svg
                      v-if="item.edits?.length"
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
                    ><span v-else class="msg-icon-placeholder"></span
                  ></span>
                </div>
                <div class="msg-body">
                  <template v-if="!isEditing(item.id)">
                    <div
                      :ref="
                        (el) => checkMessageHeight(item.id, el as HTMLElement)
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
                      v-if="!isCompactMode && item.likes?.length > 0"
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
              <div
                v-else
                class="msg-layout"
                :class="{ 'msg-layout-compact': isCompactMode }"
              >
                <!-- Avatar (only in normal mode) -->
                <router-link
                  v-if="!isCompactMode"
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
                  <div
                    class="msg-header"
                    :class="{ 'msg-header-compact': isCompactMode }"
                  >
                    <!-- Compact mode: icon + time | name + likes -->
                    <template v-if="isCompactMode">
                      <span
                        class="msg-time-group"
                        :title="formatFullDate(item)"
                      >
                        <svg
                          v-if="item.edits?.length"
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
                        ><span v-else class="msg-icon-placeholder"></span
                        ><span class="msg-time">{{
                          formatTime(item.createdUtc)
                        }}</span>
                      </span>
                      <router-link
                        :to="{
                          name: 'profile',
                          params: { login: item.author.login },
                        }"
                        class="msg-author"
                        :class="{ online: isOnline(item.author) }"
                        >{{ item.author.login }}</router-link
                      ><button
                        v-if="item.likes?.length > 0"
                        class="msg-likes-inline"
                        :class="{ 'my-like': isLikedByMe(item) }"
                        :title="getLikesTooltip(item)"
                        @click="toggleLike(item)"
                      >
                        <svg viewBox="-1.2 -0.75 26.4 26.4" fill="none">
                          <path
                            d="M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z"
                            stroke="currentColor"
                            stroke-width="2"
                          /></svg
                        >{{ item.likes.length }}</button
                      >
                    </template>
                    <!-- Normal mode: name first, then time -->
                    <template v-else>
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
                          v-if="item.edits?.length"
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
                        ><span v-else class="msg-icon-placeholder"></span>
                      </span>
                    </template>
                  </div>

                  <template v-if="!isEditing(item.id)">
                    <div
                      :ref="
                        (el) => checkMessageHeight(item.id, el as HTMLElement)
                      "
                      class="msg-content"
                      :class="{ collapsed: isTruncated(item) }"
                    >
                      <div class="msg-text" v-html="item.text" />
                    </div>
                    <div v-if="needsTruncation(item)" class="msg-expand-row">
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
                      v-if="!isCompactMode && item.likes?.length > 0"
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

        <!-- Bottom sentinel for loading newer messages -->
        <div
          v-if="hasMoreAfter"
          ref="bottomSentinel"
          class="scroll-sentinel bottom-sentinel"
        >
          <the-loader v-if="loadingAfter" :small="true" />
        </div>
      </template>
    </div>

    <!-- Toolbar (positioned inside container, clipped by overflow) -->
    <div
      v-if="
        hoveredMessage &&
        !hoveredMessage.isRemoved &&
        !isEditing(hoveredMessage.id) &&
        isToolbarVisible
      "
      class="msg-toolbar"
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
            <svg viewBox="3.1 3.1 17.8 17.8" width="20" height="20" fill="none">
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
          <a
            class="toolbar-btn"
            title="Ссылка на сообщение"
            :href="`#msg-${hoveredMessage.id}`"
            @click.prevent="copyAnchor(hoveredMessage.id)"
          >
            <svg viewBox="-2 -2 28 28" width="20" height="20" fill="none">
              <path
                d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
              <path
                d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
          </a>
        </template>
    </div>

    <!-- Scroll to latest button (centered over chat) -->
    <button v-if="hasMoreAfter" class="scroll-to-latest" @click="jumpToLatest">
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

  <!-- Input area below chat window -->
  <div class="chat-input-wrapper">
    <!-- Typing indicator -->
    <div v-if="typingUsers.length > 0" class="typing-indicator">
      <span class="typing-dots"> <span></span><span></span><span></span> </span>
      <span class="typing-text">{{ typingText }}</span>
    </div>

    <div class="chat-input-container">
      <template v-if="canSendMessages">
        <BBCodeEditor
          ref="editorRef"
          v-model="newMessage"
          context="message"
          placeholder=""
          draft-key="chat"
          :disabled="sending"
          :min-height="60"
          :max-height="200"
          :resizable="true"
          :is-moderator="isModerator"
          @submit="handleSend"
        />
        <button
          class="chat-send-button"
          :disabled="sending || !newMessage.trim()"
          @click="handleSend"
        >
          Отправить
        </button>
      </template>
      <secondary-text v-else-if="isBanned" class="chat-banned-hint">
        Вы не можете отправлять сообщения из-за ограничений аккаунта
      </secondary-text>
      <secondary-text v-else class="chat-login-hint">
        <router-link to="/login">Войдите</router-link>, чтобы отправлять
        сообщения
      </secondary-text>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"
@import "src/assets/styles/Inputs"

.chat-archive
  display: flex
  align-items: center
  justify-content: space-between
  margin-bottom: $small
  color: $text

.archive-dates
  flex: 1

.archive-label
  margin-right: $tiny

.archive-link
  &:hover
    text-decoration: underline

.archive-sep
  color: $text

.view-toggle
  display: flex
  border-bottom: 2px solid $border
  position: relative

.view-toggle-btn
  display: flex
  align-items: center
  justify-content: center
  padding: $tiny $small
  border: none
  background: none
  color: $text-muted
  cursor: pointer
  transition: filter 0.2s ease

  &:hover:not(.active)
    filter: brightness($hover-brightness)

  &.active
    filter: brightness($hover-brightness)
    cursor: default

.view-indicator
  position: absolute
  bottom: -2px
  height: 2px
  background-color: $text-muted
  transition: left 0.25s ease, width 0.25s ease
  pointer-events: none

.chat-container
  display: flex
  flex-direction: column
  min-height: 400px
  position: relative
  border: 1px dashed
  border-color: $border
  overflow: hidden  // Clips toolbar when outside bounds

  // Compact mode overrides
  &.compact-mode
    .chat-message
      padding: $tiny $small $tiny $small
      margin-bottom: $small

      // Deleted collapsed messages - less margin since no content
      &.deleted-collapsed
        margin-bottom: $tiny

      // No continuation difference in compact mode
      &.continuation
        margin-top: 0

    .msg-layout-compact
      display: block

    .msg-body
      display: block

    .msg-header-compact
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
      min-width: 62px  // icon 16px + gap 6px + time ~40px

    .msg-author
      display: inline

    // Content on new line, aligned with author name
    .msg-content
      display: block
      margin-left: calc(62px + #{$small})  // time-group width + gap
      margin-top: 0

    // Edit form also aligned with content
    .msg-edit
      margin-left: calc(62px + #{$small})

    .msg-text
      display: block

    // Block elements - force new line in compact mode
    // (spoiler-head and nsfw-head are now block globally in _BbcodeContent.sass)
    .msg-text :deep(.bb-quote),
    .msg-text :deep(.bb-mod),
    .msg-text :deep(.bb-warning),
    .msg-text :deep(.bb-private),
    .msg-text :deep(.bb-cut-marker)
      display: block !important
      margin: 0 !important
      padding: 0
      line-height: 1.4

    .msg-text :deep(ul),
    .msg-text :deep(ol)
      display: block !important
      margin: 0 !important
      padding-left: $big
      line-height: 1.4

    // Image link styling
    .compact-image-link
      color: $link
      &:hover
        color: $link-hover

    // Reactions on new line
    .msg-reactions
      display: block
      margin-top: $tiny

    .msg-expand-row
      display: flex
      margin-top: $tiny
      margin-left: 0
      width: 100%

    // Deleted messages - same position as author name, same height as eye button
    .msg-deleted-inline
      display: inline-flex
      align-items: center
      min-height: 16px
      color: $text-muted
      font-style: italic

      &.clickable
        cursor: pointer
        &:hover
          text-decoration: underline

.chat-messages
  height: calc(100vh - 350px)
  min-height: 200px
  overflow-y: scroll
  overflow-x: hidden
  padding: 0
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
  // Disable hover effects during scroll
  &.is-scrolling .chat-message
    pointer-events: none

.chat-empty
  text-align: center
  padding: $big

.scroll-sentinel
  height: 1px
  width: 100%

.top-sentinel,
.bottom-sentinel
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

.chat-message
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
  align-items: baseline
  .msg-time-gutter
    width: 64px
    flex-shrink: 0
    display: flex
    justify-content: center
    line-height: 1
    .msg-time-hover
      opacity: 0
      transition: opacity 0.1s ease

.chat-message:hover .msg-time-gutter .msg-time-hover,
.chat-message.hovered .msg-time-gutter .msg-time-hover
  opacity: 1

.chat-message.highlighted
  animation: highlight-fade 2s ease-out forwards

.chat-message.removed
  color: $text-muted

@keyframes highlight-fade
  0%
    background-color: var(--bg-element)
  70%
    background-color: var(--bg-element)
  100%
    background-color: transparent

.msg-layout
  display: flex
  gap: $medium

.msg-layout-compact
  gap: 0

.msg-body
  flex: 1
  min-width: 0
  position: relative

.msg-header
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $tiny
  line-height: 1

.msg-avatar-link
  flex-shrink: 0
  align-self: flex-start

.msg-avatar
  width: 64px
  height: 64px
  border-radius: 50%
  object-fit: cover
  display: block

.msg-avatar-placeholder
  flex-shrink: 0
  width: 64px
  height: 64px

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
  align-items: center
  gap: 6px
  color: $text-muted

// Иконки inline (карандаш, мусорка)
.msg-edited-icon,
.msg-deleted-icon
  width: 16px
  height: 16px
  flex-shrink: 0
  color: $text-muted

// Placeholder для иконки (когда её нет)
.msg-icon-placeholder
  display: inline-block
  width: 16px
  height: 16px
  flex-shrink: 0

// Иконки в кнопках (якорь) - центрируются flexbox-ом кнопки
.msg-anchor-btn svg
  width: 16px
  height: 16px
  flex-shrink: 0

.msg-header-btn,
.msg-anchor-btn
  display: inline-flex
  align-items: center
  justify-content: center
  width: 16px
  height: 16px
  min-width: 16px
  margin: 0
  padding: 0
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted
  text-decoration: none
  font: inherit
  line-height: inherit
  box-sizing: border-box
  -webkit-appearance: none
  appearance: none
  &:hover svg
    filter: brightness($hover-brightness)

.msg-content
  color: $text
  line-height: 1.5
  position: relative

  &.collapsed
    max-height: 500px
    overflow: hidden
    &::after
      content: ""
      position: absolute
      bottom: 0
      left: 0
      right: 0
      height: 3em
      background: linear-gradient(to bottom, transparent, var(--bg-page))
      pointer-events: none

.chat-message:hover .msg-content.collapsed::after,
.chat-message.hovered .msg-content.collapsed::after
  background: linear-gradient(to bottom, transparent, var(--bg-element))

.msg-text
  :deep()
    +bbcode-content
    // Images should align to top so time aligns with first "line"
    img, .image
      vertical-align: top

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
  align-items: center
  gap: $small
  margin-top: $small
  // Extend to full width (beyond msg-body, covering avatar area)
  margin-left: calc(-64px - #{$medium})
  width: calc(100% + 64px + #{$medium})

  &::before, &::after
    content: ''
    flex: 1
    height: 0
    border-top: 1px dashed $border

.expand-toggle
  flex-shrink: 0
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
  margin-top: $small

.msg-toolbar
  position: absolute
  display: flex
  align-items: center
  gap: 0
  padding: 0
  background-color: $bg-element
  box-shadow: 0 0 0 1px var(--hover-overlay), 0 2px 8px var(--shadow-color)
  border-radius: $border-radius
  z-index: 100
  pointer-events: auto

.toolbar-btn-delete-confirm
  color: $accent-red !important
  &:hover
    color: $accent-red-hover !important

.toolbar-btn-cancel
  color: $text-muted !important
  &:hover
    color: $heading-alt !important

.toolbar-btn
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

  svg
    transition: transform 0.15s ease

  &:hover
    background-color: $bg-element-accent
    svg
      filter: brightness($hover-brightness)
      transform: scale(1.15)

  &:active
    background-color: $hover-overlay
    svg
      transform: scale(0.9)

  &.active
    color: $text-muted
    svg
      fill: currentColor
    &:hover
      background-color: $bg-element-accent
      svg
        filter: brightness($hover-brightness)

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
    width: 22px
    height: 22px
    fill: none
    transition: transform 0.15s ease
  &:hover
    background-color: $active-overlay
    svg
      filter: brightness($hover-brightness)
      transform: scale(1.15)
  &:active
    background-color: $hover-overlay
    transform: scale(0.95)
  &.my-reaction
    svg
      fill: currentColor

.chat-message:hover .reaction-badge,
.chat-message.hovered .reaction-badge
  background-color: $hover-overlay

.chat-message:hover .reaction-badge:hover,
.chat-message.hovered .reaction-badge:hover
  background-color: $active-overlay

.chat-message:hover .reaction-badge.my-reaction,
.chat-message.hovered .reaction-badge.my-reaction
  background-color: $hover-overlay

.chat-message:hover .reaction-badge.my-reaction:hover,
.chat-message.hovered .reaction-badge.my-reaction:hover
  background-color: $active-overlay

// BBCodeEditor overlay при hover на сообщение
// Overlay накладывается ПОВЕРХ базового $input-bg (не заменяет)
.chat-message:hover :deep(.bbcode-editor),
.chat-message.hovered :deep(.bbcode-editor)
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

.msg-deleted-icon-inline
  width: 16px
  height: 16px
  margin-right: 6px
  vertical-align: middle

// Inline likes in compact mode (with background like reaction-badge)
.msg-likes-inline
  display: inline-flex
  align-items: center
  gap: 4px
  padding: 2px 6px
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $hover-overlay
  color: $text-muted
  font-size: $font-size
  line-height: 1
  cursor: pointer
  vertical-align: baseline
  transition: background-color 0.1s ease
  svg
    width: 16px
    height: 16px
    fill: none
    flex-shrink: 0
    transition: transform 0.15s ease
  &:hover
    background-color: $active-overlay
    svg
      filter: brightness($hover-brightness)
      transform: scale(1.1)
  &.my-like svg
    fill: currentColor

// Triangle toggle for deleted messages
.msg-triangle
  color: $text-muted
  font-size: 10px
  margin-right: 4px
  vertical-align: middle
  user-select: none

.msg-triangle-clickable
  cursor: pointer
  &:hover
    filter: brightness($hover-brightness)

// "(скрыть)" link
.msg-hide-link
  color: $text-muted
  font-size: 16px
  text-decoration: none
  &:hover
    text-decoration: underline
    filter: brightness($hover-brightness)

.chat-input-wrapper
  margin-top: $medium

.typing-indicator
  display: flex
  align-items: center
  gap: $tiny
  padding: 2px 0
  margin-bottom: $small
  font-size: 11px
  color: $text-muted

.typing-dots
  display: inline-flex
  gap: 2px
  span
    width: 4px
    height: 4px
    border-radius: 50%
    background-color: $text-muted
    animation: typing-bounce 1.4s infinite ease-in-out both
    &:nth-child(1)
      animation-delay: 0s
    &:nth-child(2)
      animation-delay: 0.16s
    &:nth-child(3)
      animation-delay: 0.32s

@keyframes typing-bounce
  0%, 80%, 100%
    transform: scale(0.6)
    opacity: 0.4
  40%
    transform: scale(1)
    opacity: 1

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

.chat-input-container
  display: flex
  flex-direction: column
  gap: $small
  width: 100%

  :deep(.bbcode-editor-wrapper)
    width: 100%

.chat-send-button
  +button()
  align-self: flex-start

.chat-login-hint
  flex: 1
  text-align: center
  padding: $small
  a
    color: $link
    &:hover
      text-decoration: underline

.chat-banned-hint
  flex: 1
  text-align: center
  padding: $small
  color: $accent-red
</style>
