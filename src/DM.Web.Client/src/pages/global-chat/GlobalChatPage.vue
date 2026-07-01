<script setup lang="ts">
import { onMounted, onUnmounted, ref, nextTick, computed, watch } from "vue";
import { useRouter, useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import {
  useGlobalChatStore,
  type GlobalChatMessage,
} from "@/entities/global-chat";
import { useUserStore } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import { AccessPolicy } from "@/shared/api/models/community";
import { Tooltip } from "@/shared/ui/Tooltip";
import dayjs from "dayjs";
import { symbols } from "@/shared/lib/utils/icons";
import { SvgIcon } from "@/shared/ui/Icon";
import { DatePicker } from "@/shared/ui/DatePicker";
import { BBCodeEditor } from "@/features/editor";
import { globalChatApi } from "@/entities/global-chat";
import { ChatMessage } from "@/widgets/chat-message";
import ChatEventBanner from "./ChatEventBanner.vue";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";
import {
  groupMessagesWithSeparators,
  isDateSeparator,
  isUserOnline,
  type MessageOrSeparator,
} from "@/shared/lib/utils/chat";
import {
  useMessagePermissions,
  useVirtualScroll,
} from "@/shared/lib/composables";

const router = useRouter();
const route = useRoute();
const globalChatStore = useGlobalChatStore();
const userStore = useUserStore();
const {
  messages,
  loading,
  error,
  sending,
  hasMoreBefore,
  hasMoreAfter,
  highlightedMessageId,
} = storeToRefs(globalChatStore);
const { user } = storeToRefs(userStore);
const { isCompactLayout } = storeToRefs(useUiStore());

const MAX_MESSAGE_HEIGHT = 500;

// Message permissions (shared composable)
const {
  isModerator,
  canEdit: canEditMsg,
  canDelete: canDeleteMsg,
  canLike: canLikeMsg,
} = useMessagePermissions(user);

const isBanned = computed(() => {
  if (!user.value?.accessPolicy) return false;
  const policy = user.value.accessPolicy;
  return (
    policy === AccessPolicy.DemocraticBan || policy === AccessPolicy.FullBan
  );
});

// isModerator provided by useMessagePermissions above

const canSendMessages = computed(() => user.value && !isBanned.value);

// Typing indicator (placeholder - will be connected to WebSocket later)
const typingUsers = ref<string[]>([]);
const typingText = computed(() => {
  if (typingUsers.value.length === 0) return "";
  if (typingUsers.value.length === 1)
    return `${typingUsers.value[0]} печатает...`;
  if (typingUsers.value.length === 2)
    return `${typingUsers.value[0]} и ${typingUsers.value[1]} печатают...`;
  return `${typingUsers.value[0]} и еще ${typingUsers.value.length - 1} печатают...`;
});

const newMessage = ref("");
const globalChatContainer = ref<HTMLElement | null>(null);
const messagesContainer = ref<HTMLElement | null>(null);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const editEditorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const topSentinel = ref<HTMLElement | null>(null);
const bottomSentinel = ref<HTMLElement | null>(null);

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
        await globalChatStore.fetchMoreBefore();

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
        if (
          isInitialScrolling ||
          !entries[0].isIntersecting ||
          isLoadingNewer ||
          !hasMoreAfter.value
        )
          return;
        isLoadingNewer = true;

        await globalChatStore.fetchMoreAfter();
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
  return globalChatStore.findMessageInLoaded(hoveredMessageId.value);
});

// Toolbar is always visible when message is hovered - clipping is handled by CSS
const isToolbarVisible = computed(() => {
  return !!(hoveredMessage.value && globalChatContainer.value);
});

function handleMessageMouseEnter(event: MouseEvent, msgId: string) {
  if (isScrolling.value) return;
  if (hideToolbarTimeout) {
    clearTimeout(hideToolbarTimeout);
    hideToolbarTimeout = null;
  }

  const target = event.currentTarget as HTMLElement;
  const rect = target.getBoundingClientRect();
  const container = globalChatContainer.value;

  if (!container) return;

  const containerRect = container.getBoundingClientRect();

  // Calculate position relative to globalChat-container
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
const expandedDeletedMessages = ref<Set<string>>(new Set());

// Group messages with date separators (uses shared utility)
const messagesWithSeparators = computed((): MessageOrSeparator[] =>
  groupMessagesWithSeparators(messages.value ?? []),
);

// isDateSeparator imported from shared/lib/utils/chat

// Virtual scroll for message list
const itemCount = computed(() => messagesWithSeparators.value.length);
const { virtualItems, totalSize, measureElement, scrollToIndex } =
  useVirtualScroll({
    count: itemCount,
    container: messagesContainer,
    estimateSize: 80,
    overscan: 15,
  });

// Today (capped maximum for the date picker, YYYY-MM-DD)
const todayValue = dayjs().format("YYYY-MM-DD");

// Currently selected archive date (drives the DatePicker)
const selectedDate = ref("");

// User picked a date from the DatePicker — push to the URL; the
// route.query.date watcher performs the actual load (single code path).
function onDatePicked(value: string) {
  if (!value) return;
  router.push({ name: "global-chat", query: { date: value } });
}

// Replace images with links in compact layout
function replaceImagesWithLinks(container: HTMLElement | null) {
  if (!container || !isCompactLayout.value) return;

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

// Restore images when leaving compact layout
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

// Watch compact layout changes
watch(isCompactLayout, (newValue) => {
  nextTick(() => {
    if (newValue) {
      replaceImagesWithLinks(messagesContainer.value);
    } else {
      restoreImages(messagesContainer.value);
    }
  });
});

// Watch for new messages to init interactive BBCode elements
// Only watch message count changes, not deep properties (performance)
watch(
  () => messages.value?.length,
  () => {
    nextTick(() => {
      initBbcodeInteractive(messagesContainer.value);
      if (isCompactLayout.value) {
        replaceImagesWithLinks(messagesContainer.value);
      }
    });
  },
);

onMounted(async () => {
  const hashMsgId = getHashMessageId();
  const dateQuery =
    typeof route.query.date === "string" ? route.query.date : null;

  if (hashMsgId) {
    isInitialScrolling = true;
    await globalChatStore.navigateToMessage(hashMsgId);
    nextTick(() => {
      scrollToMessage(hashMsgId);
      setupInfiniteScroll();
      initBbcodeInteractive(messagesContainer.value);
      // Allow infinite scroll after animation completes
      setTimeout(() => {
        isInitialScrolling = false;
      }, 600);
    });
  } else if (dateQuery) {
    await loadArchiveDate(dateQuery);
    nextTick(() => {
      setupInfiniteScroll();
      initBbcodeInteractive(messagesContainer.value);
    });
  } else {
    await globalChatStore.fetchMessages();
    scrollToBottom();
    nextTick(() => {
      setupInfiniteScroll();
      initBbcodeInteractive(messagesContainer.value);
    });
  }

  // Apply image replacement if in compact layout
  if (isCompactLayout.value) {
    replaceImagesWithLinks(messagesContainer.value);
  }
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

// React to ?date changes (archive quick links, picker, back/forward).
// onMounted performs the very first load, so this fires only on later changes.
watch(
  () => route.query.date,
  (newDate, oldDate) => {
    if (newDate === oldDate) return;
    if (typeof newDate === "string" && newDate) {
      loadArchiveDate(newDate);
    } else {
      // Date cleared — return to latest.
      selectedDate.value = "";
      globalChatStore.jumpToLatest().then(scrollToBottom);
    }
  },
);

// Track latest activity per username
// Using ISO string comparison (lexicographic) instead of dayjs for performance
const latestActivityByUsername = computed(() => {
  const map = new Map<string, string>();
  if (!messages.value?.length) return map;
  for (const msg of messages.value) {
    if (!msg.author?.username || !msg.author?.lastActivityUtc) continue;
    const existing = map.get(msg.author.username);
    // ISO 8601 strings compare correctly lexicographically
    if (!existing || msg.author.lastActivityUtc > existing) {
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

// Permissions — canEditMsg, canDeleteMsg, canLikeMsg from useMessagePermissions above

function canEditMessage(msg: GlobalChatMessage) {
  return canEditMsg(msg);
}

function canDeleteMessage(msg: GlobalChatMessage) {
  return canDeleteMsg(msg);
}

function canLikeMessage(msg: GlobalChatMessage) {
  return canLikeMsg(msg);
}

function isLikedByMe(msg: GlobalChatMessage) {
  if (!user.value) return false;
  return (
    msg.likes?.some((u: any) => u.username === user.value?.username) ?? false
  );
}

// Edit
function isEditing(msgId: string) {
  return editingId.value === msgId;
}

async function startEdit(msg: GlobalChatMessage) {
  editingId.value = msg.id;
  // Fetch the original BBCode from the backend
  const { data } = await globalChatApi.getMessageForEdit(msg.id);
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
  // Reinitialize interactive elements after DOM update
  nextTick(() => {
    initBbcodeInteractive(messagesContainer.value);
    if (isCompactLayout.value) {
      replaceImagesWithLinks(messagesContainer.value);
    }
  });
}

async function saveEdit(msgId: string) {
  if (editText.value.trim()) {
    await globalChatStore.updateMessage(msgId, editText.value);
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
async function toggleLike(msg: GlobalChatMessage) {
  if (!user.value) return;
  if (isLikedByMe(msg)) {
    await globalChatStore.unlikeMessage(msg.id);
  } else {
    await globalChatStore.likeMessage(msg.id);
  }
}

// Anchor
function copyAnchor(msgId: string) {
  const url = `${window.location.origin}${window.location.pathname}#msg-${msgId}`;
  navigator.clipboard.writeText(url);
}

function scrollToMessage(msgId: string) {
  // With virtual scroll, element may not be in DOM yet — scroll virtualizer first
  const index = messagesWithSeparators.value.findIndex(
    (item) => !isDateSeparator(item) && (item as any).id === msgId,
  );
  if (index >= 0) {
    scrollToIndex(index, { align: "center" });
  }
  // After virtualizer scrolls and renders, find DOM element for highlight
  nextTick(() => {
    setTimeout(() => {
      const element = document.getElementById(`msg-${msgId}`);
      if (element) {
        element.classList.add("highlighted");
        setTimeout(() => {
          element.classList.remove("highlighted");
          globalChatStore.clearHighlight();
        }, 1500);
      }
    }, 100);
  });
}

function getHashMessageId(): string | null {
  const hash = window.location.hash;
  if (hash && hash.startsWith("#msg-")) {
    return hash.slice(5);
  }
  return null;
}

// Load messages for an archive date and scroll to the top of the result.
// The URL (?date=YYYY-MM-DD) is the single source of truth — callers change
// the route, the route.query.date watcher routes here.
async function loadArchiveDate(date: string) {
  selectedDate.value = date;
  await globalChatStore.navigateToDate(date);
  nextTick(() => {
    scrollToIndex(0, { align: "start" });
  });
}

// Jump to latest. Clearing the date query lets the watcher load latest;
// when no date query is present, load directly.
async function jumpToLatest() {
  selectedDate.value = "";
  if (route.query.date) {
    router.replace({ name: "global-chat", query: {} });
    return;
  }
  await globalChatStore.jumpToLatest();
  scrollToBottom();
}

function scrollToBottom() {
  nextTick(() => {
    if (messagesContainer.value) {
      messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight;
    }
  });
}

// Retry after a load error — re-run the load path for the current view.
async function retryLoad() {
  const dateQuery =
    typeof route.query.date === "string" ? route.query.date : null;
  if (dateQuery) {
    await loadArchiveDate(dateQuery);
  } else {
    await globalChatStore.fetchMessages();
    scrollToBottom();
  }
}

async function handleSend() {
  if (!newMessage.value.trim() || sending.value) return;
  const text = newMessage.value;
  newMessage.value = "";
  editorRef.value?.clear();
  await globalChatStore.sendMessage(text);
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
    await globalChatStore.deleteMessage(confirmingDeleteId.value);
    confirmingDeleteId.value = null;
  }
}
</script>

<template>
  <page-title v-once>Глобальный чат</page-title>

  <!-- One row: the active chat event (left, read-only for guests) and the
       "jump to a past day" date picker (right). -->
  <div class="globalChat-topbar">
    <ChatEventBanner />
    <DatePicker
      class="globalChat-datepicker"
      :model-value="selectedDate"
      :max="todayValue"
      label="Перейти к дате"
      @update:model-value="onDatePicked"
    />
  </div>

  <div
    ref="globalChatContainer"
    class="globalChat-container"
    :class="{ 'layout-compact': isCompactLayout }"
  >
    <div
      ref="messagesContainer"
      class="globalChat-messages"
      :class="{
        'is-scrolling': isScrolling,
        'is-empty': !loading && !messages?.length,
      }"
      @scroll="handleScroll"
      @wheel.passive="handleWheel"
    >
      <secondary-text v-if="loading" class="globalChat-empty">
        Загрузка сообщений...
      </secondary-text>
      <!-- Error takes precedence over fake-empty; stale content (if any) is preserved -->
      <div
        v-else-if="error && !messages?.length"
        class="globalChat-empty globalChat-error"
      >
        <secondary-text>{{ error }}</secondary-text>
        <button type="button" class="globalChat-retry" @click="retryLoad">
          Повторить
        </button>
      </div>
      <secondary-text v-else-if="!messages?.length" class="globalChat-empty">
        <template v-if="canSendMessages"
          >Сообщений пока нет. Начните общение!</template
        >
        <template v-else>Сообщений пока нет.</template>
      </secondary-text>
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
            v-for="vRow in virtualItems"
            :key="String(vRow.key)"
            :ref="
              (el) => {
                if (el) measureElement(el as HTMLElement);
              }
            "
            :data-index="vRow.index"
            :style="{
              position: 'absolute',
              top: 0,
              left: 0,
              width: '100%',
              transform: `translateY(${vRow.start}px)`,
            }"
          >
            <div
              v-if="isDateSeparator(messagesWithSeparators[vRow.index])"
              class="date-separator"
            >
              <div class="separator-line"></div>
              <span class="separator-text">{{
                (messagesWithSeparators[vRow.index] as any).formattedDate
              }}</span>
              <div class="separator-line"></div>
            </div>

            <div
              v-else
              :id="`msg-${(messagesWithSeparators[vRow.index] as any).id}`"
              :data-id="(messagesWithSeparators[vRow.index] as any).id"
              class="globalChat-message"
              :class="{
                removed: (messagesWithSeparators[vRow.index] as any).isRemoved,
                hovered:
                  hoveredMessageId ===
                  (messagesWithSeparators[vRow.index] as any).id,
                continuation: (messagesWithSeparators[vRow.index] as any)
                  .isContinuation,
                'deleted-collapsed':
                  (messagesWithSeparators[vRow.index] as any).isRemoved &&
                  !isDeletedExpanded(
                    (messagesWithSeparators[vRow.index] as any).id,
                  ),
              }"
              @mouseenter="
                handleMessageMouseEnter(
                  $event,
                  (messagesWithSeparators[vRow.index] as any).id,
                )
              "
              @mouseleave="handleMessageMouseLeave"
            >
              <ChatMessage
                :message="messagesWithSeparators[vRow.index] as any"
                :compact="isCompactLayout"
                :hovered="
                  hoveredMessageId ===
                  (messagesWithSeparators[vRow.index] as any).id
                "
                :is-online="
                  isOnline((messagesWithSeparators[vRow.index] as any).author)
                "
                :is-liked-by-me="
                  isLikedByMe(messagesWithSeparators[vRow.index] as any)
                "
                :can-edit="
                  canEditMessage(messagesWithSeparators[vRow.index] as any)
                "
                :can-delete="
                  canDeleteMessage(messagesWithSeparators[vRow.index] as any)
                "
                :can-like="
                  canLikeMessage(messagesWithSeparators[vRow.index] as any)
                "
                :is-moderator="isModerator"
                :is-editing="
                  isEditing((messagesWithSeparators[vRow.index] as any).id)
                "
                :edit-text="editText"
                :is-deleted-expanded="
                  isDeletedExpanded(
                    (messagesWithSeparators[vRow.index] as any).id,
                  )
                "
                :max-height="MAX_MESSAGE_HEIGHT"
                @like="toggleLike(messagesWithSeparators[vRow.index] as any)"
                @toggle-deleted="
                  toggleDeletedExpand(
                    (messagesWithSeparators[vRow.index] as any).id,
                  )
                "
                @start-edit="
                  startEdit(messagesWithSeparators[vRow.index] as any)
                "
                @save-edit="
                  saveEdit((messagesWithSeparators[vRow.index] as any).id)
                "
                @cancel-edit="cancelEdit"
                @update:edit-text="editText = $event"
              />
            </div>
          </div>
        </div>

        <!-- Bottom sentinel for loading newer messages -->
        <div
          v-if="hasMoreAfter"
          ref="bottomSentinel"
          class="scroll-sentinel bottom-sentinel"
        ></div>
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
        <Tooltip text="Подтвердить удаление">
          <button
            class="toolbar-btn toolbar-btn-delete-confirm"
            @click="confirmDelete"
          >
            <SvgIcon name="trash" />
          </button>
        </Tooltip>
        <Tooltip text="Отмена">
          <button class="toolbar-btn toolbar-btn-cancel" @click="cancelDelete">
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
        <Tooltip text="Ссылка на сообщение">
          <a
            class="toolbar-btn"
            :href="`#msg-${hoveredMessage.id}`"
            aria-label="Скопировать ссылку на сообщение"
            @click.prevent="copyAnchor(hoveredMessage.id)"
          >
            <SvgIcon name="anchor" />
          </a>
        </Tooltip>
      </template>
    </div>

    <!-- Scroll to latest button (centered over globalChat) -->
    <button
      v-if="hasMoreAfter"
      class="scroll-to-latest"
      aria-label="К последним сообщениям"
      @click="jumpToLatest"
    >
      <SvgIcon name="chevronDown" />
    </button>
  </div>

  <!-- Input area below globalChat window -->
  <div class="globalChat-input-wrapper">
    <!-- Typing indicator -->
    <div v-if="typingUsers.length > 0" class="typing-indicator">
      <span class="typing-dots"> <span></span><span></span><span></span> </span>
      <span class="typing-text">{{ typingText }}</span>
    </div>

    <div class="globalChat-input-container">
      <template v-if="canSendMessages">
        <BBCodeEditor
          ref="editorRef"
          v-model="newMessage"
          context="message"
          placeholder=""
          draft-key="global-chat"
          :disabled="sending"
          :min-height="60"
          :max-height="200"
          :resizable="true"
          :is-moderator="isModerator"
          @submit="handleSend"
        />
        <button
          class="globalChat-send-button"
          :disabled="sending || !newMessage.trim()"
          @click="handleSend"
        >
          Отправить
        </button>
      </template>
      <secondary-text v-else-if="isBanned" class="globalChat-banned-hint">
        Вы не можете отправлять сообщения из-за ограничений аккаунта
      </secondary-text>
      <secondary-text v-else class="globalChat-login-hint">
        <router-link to="/?action=login">Войдите</router-link>, чтобы отправлять
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
@import "src/assets/styles/ZIndex"

.globalChat-topbar
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $small

// Always pinned right, even when no event renders on the left.
.globalChat-datepicker
  margin-left: auto

.globalChat-container
  display: flex
  flex-direction: column
  min-height: 400px
  position: relative
  border: 1px dashed
  border-color: $border
  overflow: hidden  // Clips toolbar when outside bounds

  // Compact display — wrapper overrides (own element, no :deep needed)
  &.layout-compact
    .globalChat-message
      padding: $tiny $small $tiny $small
      margin-bottom: $small
      &.deleted-collapsed
        margin-bottom: $tiny
      &.continuation
        margin-top: 0

.globalChat-messages
  height: calc(100vh - 350px)
  min-height: 200px
  overflow-y: auto
  overflow-x: hidden
  padding: 0
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
  // Disable hover effects during scroll
  &.is-scrolling .globalChat-message
    pointer-events: none
  // Hide scrollbar when empty
  &.is-empty
    overflow: hidden

.globalChat-empty
  display: flex
  align-items: center
  justify-content: center
  height: 100%
  text-align: center
  padding: $big

.globalChat-error
  flex-direction: column
  gap: $small

.globalChat-retry
  +button

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

.globalChat-message
  // Full layout: comfortable horizontal container padding (compact overrides below)
  padding: $small $medium
  margin-bottom: $medium
  word-break: break-word
  overflow-wrap: break-word
  position: relative
  overflow: visible
  background-color: $bg-page
  transform: translateZ(0)
  backface-visibility: hidden

  &.continuation
    margin-bottom: $tiny
    margin-top: -$small

  &:hover,
  &.hovered
    background-color: $bg-element
    border-radius: 0 $border-radius $border-radius 0



// Parent states (own elements, no :deep needed)
.globalChat-message.highlighted
  animation: highlight-fade 2s ease-out forwards

.globalChat-message.removed
  color: $text-muted

@keyframes highlight-fade
  0%
    background-color: var(--bg-element)
  70%
    background-color: var(--bg-element)
  100%
    background-color: transparent



.msg-toolbar
  position: absolute
  display: flex
  align-items: center
  gap: 0
  padding: 0
  background-color: $bg-element
  box-shadow: 0 0 0 1px var(--hover-overlay), 0 2px 8px var(--shadow-color)
  border-radius: $border-radius
  z-index: $z-dropdown
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

// BBCodeEditor overlay при hover на сообщение
// Overlay накладывается ПОВЕРХ базового $input-bg (не заменяет)
.globalChat-message:hover :deep(.bbcode-editor),
.globalChat-message.hovered :deep(.bbcode-editor)
  background: linear-gradient($hover-overlay, $hover-overlay), $input-bg

.globalChat-input-wrapper
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
  transition: transform 0.15s ease
  box-shadow: 0 2px 8px $shadow-color
  z-index: 10
  &:hover
    background-color: $bg-element-accent
    color: $text
    transform: translateX(-50%) scale(1.05)

.globalChat-input-container
  display: flex
  flex-direction: column
  gap: $small
  width: 100%

  :deep(.bbcode-editor-wrapper)
    width: 100%

.globalChat-send-button
  align-self: flex-start
  +button

.globalChat-login-hint
  flex: 1
  text-align: center
  padding: $small
  a
    color: $link
    &:hover
      text-decoration: underline

.globalChat-banned-hint
  flex: 1
  text-align: center
  padding: $small
  color: $accent-red
</style>
