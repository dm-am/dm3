<script setup lang="ts">
import {
  onMounted,
  onUnmounted,
  ref,
  reactive,
  nextTick,
  computed,
  watch,
} from "vue";
import { useRouter, useRoute } from "vue-router";
import { useModal } from "vue-final-modal";
import { storeToRefs } from "pinia";
import {
  useGlobalChatStore,
  type GlobalChatMessage,
} from "@/entities/global-chat";
import { useAuthStore, useMessagePermissions } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import { Tooltip } from "@/shared/ui/Tooltip";
import dayjs from "dayjs";
import { symbols } from "@/shared/lib/utils/icons";
import { SvgIcon } from "@/shared/ui/Icon";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { globalChatApi } from "@/entities/global-chat";
import { ChatMessage } from "@/widgets/chat-message";
import ChatEventsPanel from "./ChatEventsPanel.vue";
import { ChatMessageSkeleton } from "@/shared/ui/Skeleton";
import { MessageSearchPanel } from "@/features/message-search";
import { WarningDialog } from "@/features/moderation-actions";
import { LoginPrompt } from "@/features/auth";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";
import { notifyFailure } from "@/shared/lib/errors";
import {
  groupMessagesWithSeparators,
  isDateSeparator,
  isUserOnline,
  type MessageOrSeparator,
} from "@/shared/lib/utils/chat";
import { useVirtualScroll } from "@/shared/lib/composables";
import { useToast } from "@/shared/lib/composables/useToast";
import { useGlobalSignalR } from "@/shared/lib/composables/useSignalR";
import { NotificationType } from "@/shared/api/models/notifications";
import type { SignalRNotification } from "@/shared/api/models/notifications";
import { useMessageToolbar } from "@/shared/lib/composables/useMessageToolbar";
import {
  useAnchoredInfiniteScroll,
  LANDING_SCROLL_MS,
} from "@/shared/lib/composables/useAnchoredInfiniteScroll";

const router = useRouter();
const route = useRoute();
const globalChatStore = useGlobalChatStore();
const userStore = useAuthStore();
const toast = useToast();
const {
  messages,
  loading,
  error,
  errorBefore,
  errorAfter,
  sending,
  hasMoreBefore,
  hasMoreAfter,
  highlightedMessageId,
  landedOnRequestedDate,
  liveEvent,
  eventDetails,
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

// isModerator provided by useMessagePermissions above

const canSendMessages = computed(() => Boolean(user.value));

// ─────────────────────────────────────────────────────────────
// Live event details + closed event restriction hint
// ─────────────────────────────────────────────────────────────
// The in-frame live banner needs full details for ANY live event (duration
// for "идет до HH:mm" + the expandable description), and during a Live
// event with isOpen=false the backend additionally rejects messages from
// non-participants (see MessageService) — the participants list from the
// same details response feeds the quiet hint above the input. Single
// owner: the page fetches once into the shared store cache, the banner and
// the hint both read from it.
watch(
  liveEvent,
  (ev) => {
    if (ev) {
      globalChatStore.fetchEventDetails(ev.id);
    }
  },
  { immediate: true },
);

const showClosedEventHint = computed(() => {
  const ev = liveEvent.value;
  if (!ev || ev.isOpen || !user.value || !canSendMessages.value) return false;
  // Details not loaded yet — assume no restriction to avoid a false flash.
  const details = eventDetails.value[ev.id];
  if (!details) return false;
  return !details.participants?.some(
    (p) => p.user.username === user.value?.username,
  );
});

const newMessage = ref("");
const globalChatContainer = ref<HTMLElement | null>(null);
const messagesContainer = ref<HTMLElement | null>(null);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const topSentinel = ref<HTMLElement | null>(null);
const bottomSentinel = ref<HTMLElement | null>(null);
const toolbarEl = ref<HTMLElement | null>(null);

// Scroll position tracking
const isScrolling = ref(false);
let scrollEndTimeout: ReturnType<typeof setTimeout> | null = null;

// Infinite scroll: shared with the messenger. Both ends page here — the feed
// can sit in the middle of the history (archive dates, permalinks), so newer
// messages are as fetchable as older ones. errorBefore/errorAfter park the
// direction that failed until the sentinel's "Повторить" is clicked.
const {
  loadOlder,
  loadNewer,
  setupInfiniteScroll,
  suspend: suspendInfiniteScroll,
  resume: resumeInfiniteScroll,
} = useAnchoredInfiniteScroll({
  container: messagesContainer,
  older: {
    sentinel: topSentinel,
    hasMore: () => hasMoreBefore.value,
    load: () => globalChatStore.fetchMoreBefore(),
    failed: () => Boolean(errorBefore.value),
  },
  newer: {
    sentinel: bottomSentinel,
    hasMore: () => hasMoreAfter.value,
    load: () => globalChatStore.fetchMoreAfter(),
    failed: () => Boolean(errorAfter.value),
  },
});

// autoGrowEdit removed - BBCodeEditor handles its own sizing

// Edit state — editText holds the message's original BBCode, used only to
// seed ChatMessage's editor on entering edit mode (:edit-text is consumed
// as an initial value there, not synced back). The actual edited text lives
// inside ChatMessage's own local editor state and travels back to us as the
// @save-edit payload — never read from this ref again after startEdit.
const editingId = ref<string | null>(null);
const editText = ref("");

// Hover toolbar: shared with the messenger, which is what keeps the keyboard
// path below from existing in one chat and not the other.
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
  container: globalChatContainer,
  toolbar: toolbarEl,
  isScrolling: () => isScrolling.value,
});

const hoveredMessage = computed(() => {
  if (!hoveredMessageId.value) return null;
  return globalChatStore.findMessageInLoaded(hoveredMessageId.value);
});

// Toolbar is always visible when message is hovered - clipping is handled by CSS
const isToolbarVisible = computed(() => {
  return !!(hoveredMessage.value && globalChatContainer.value);
});

function handleWheel() {
  isScrolling.value = true;
  handleScrollStart();
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
  updateAtBottom();
  // The ARCHIVE -> LIVE auto-exit is driven by the actual scroll reaching
  // the bottom ("при докрутке до низа"), not only by ref-change watchers —
  // guards inside make this a cheap no-op outside archive mode.
  maybeExitArchiveToLive();
  if (scrollEndTimeout) {
    clearTimeout(scrollEndTimeout);
  }
  scrollEndTimeout = setTimeout(() => {
    isScrolling.value = false;
    scrollEndTimeout = null;
  }, 150);
}

// ─────────────────────────────────────────────────────────────
// Bottom-of-chat state machine
// ─────────────────────────────────────────────────────────────
// The feed lives in one of two modes; the ?date query param is the single
// source of truth for which one:
//
// LIVE (no ?date, hasMoreAfter=false): the loaded window ends at the newest
//   message.
//   - New messages (SignalR push / poll fallback) append to the tail. If
//     the reader is at the bottom, the view follows them (autoscroll); if
//     they scrolled up to read history, the viewport is left alone.
//   - The scroll-to-latest button shows only while scrolled up (then there
//     is actually somewhere to jump); clicking it just scrolls — the tail
//     is already loaded, no refetch, no URL change.
//
// ARCHIVE (?date=YYYY-MM-DD): the window starts at the picked day.
//   - Intermediate page (hasMoreAfter=true): the bottom sentinel keeps
//     paging toward "now"; the button stays visible and jumps straight to
//     the latest messages (clears ?date, reloads the tail).
//   - Tail page (hasMoreAfter=false): reaching the actual bottom means the
//     reader has caught up with the present — the page auto-exits to LIVE:
//     ?date is removed via router.replace (no reload — the loaded window
//     already IS the tail; suppressNextDateWatch skips the route watcher),
//     live appends resume seamlessly and the button disappears. Until that
//     bottom is reached the button stays (still somewhere to jump).
//
// Transitions: LIVE -> ARCHIVE only via the date picker / URL. ARCHIVE ->
// LIVE via the button, via the auto-exit above, or via clearing ?date
// manually (back/forward included).
//
// isAtBottom: whether the viewport is scrolled to (near) the bottom of the
// loaded window. Drives the button visibility and the autoscroll-vs-stay
// decision; it is re-measured (not trusted) before the auto-exit fires,
// because appending a page grows scrollHeight without a scroll event.
const isAtBottom = ref(true);
const AT_BOTTOM_THRESHOLD_PX = 40;

// CHAT-10: the scroll-to-latest button appears only after a DEEP departure
// from the bottom (a full chat viewport up), not on the first wheel tick,
// and hides again once the reader is (nearly) back at the bottom. The gap
// between the two thresholds is deliberate hysteresis — no flicker while
// hovering around either edge.
const isFarFromBottom = ref(false);

function updateAtBottom() {
  const container = messagesContainer.value;
  if (!container) return;
  const distanceFromBottom =
    container.scrollHeight - container.scrollTop - container.clientHeight;
  isAtBottom.value = distanceFromBottom < AT_BOTTOM_THRESHOLD_PX;
  if (distanceFromBottom > container.clientHeight) {
    isFarFromBottom.value = true;
  } else if (isAtBottom.value) {
    isFarFromBottom.value = false;
  }
}

// ARCHIVE -> LIVE auto-exit (see state machine above): fires when the
// reader is at the real bottom of the last available page while an archive
// date is selected. Re-measures after the DOM settles so a freshly appended
// page (which grows scrollHeight without a scroll event) can't fake
// "at bottom".
watch([isAtBottom, hasMoreAfter], () => {
  maybeExitArchiveToLive();
});

function maybeExitArchiveToLive() {
  if (!selectedDate.value || hasMoreAfter.value || loading.value) return;
  nextTick(() => {
    updateAtBottom();
    if (!selectedDate.value || hasMoreAfter.value || !isAtBottom.value) return;
    selectedDate.value = "";
    if (route.query.date) {
      suppressNextDateWatch = true;
      router.replace({ name: "global-chat", query: {} });
    }
  });
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
// Stable per-item key (message id / date-separator date) instead of the
// virtualizer's default index-based key — prevents measurements and hover/
// highlight state from sliding onto a different message when older history
// is prepended (fetchMoreBefore shifts every existing index).
function chatItemKey(index: number): string {
  const item = messagesWithSeparators.value[index];
  if (!item) return String(index);
  return isDateSeparator(item) ? `sep-${item.date}` : item.id;
}
const { virtualItems, totalSize, measureElement, scrollToIndex } =
  useVirtualScroll({
    count: itemCount,
    container: messagesContainer,
    estimateSize: 80,
    overscan: 15,
    getItemKey: chatItemKey,
  });

// Today (capped maximum for the date picker, YYYY-MM-DD)
const todayValue = dayjs().format("YYYY-MM-DD");

// Currently selected archive date (drives the DatePicker)
const selectedDate = ref("");

// Set by loadArchiveDate right before it corrects the URL after a
// fallback-to-latest (see there) — the route.query.date watcher checks this
// to skip its own reload, since loadArchiveDate already has fresh messages
// loaded and a second fetchMessages()/jumpToLatest() would be redundant.
let suppressNextDateWatch = false;

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

// ─────────────────────────────────────────────────────────────
// Realtime updates
// ─────────────────────────────────────────────────────────────
// True when the currently loaded window is the tail of the chat (no archive
// date selected and no newer page to fetch) — the only state where a
// pushed/polled new message should be appended live instead of silently
// dropped (store.addMessage already no-ops otherwise).
const isAtLatest = computed(() => !selectedDate.value && !hasMoreAfter.value);

// SignalR push: the backend broadcasts NotificationType.NewGlobalChatMessage to
// every open connection — guests included (the hub accepts anonymous
// connections as receive-only broadcast listeners). The payload
// intentionally omits message text (BBCode rendering stays server-side), so
// on receipt we just fetch/append via the store's existing dedupe-by-id
// path, same as the polling fallback below.
//
// The page holds its own lease on the shared connection: for guests it is
// the sole owner (created on mount, released on unmount); for authenticated
// users connect() reuses the App.vue-owned socket and releasing the page
// lease on unmount leaves that socket untouched.
const {
  connect: connectChatSignalR,
  disconnect: disconnectChatSignalR,
  onNotification: onGlobalNotification,
  isConnected: isSignalRConnected,
} = useGlobalSignalR("global-chat-page");

async function fetchNewMessages() {
  if (!isAtLatest.value) return;
  const before = messages.value?.length ?? 0;
  // Capture BEFORE the fetch: appending grows scrollHeight without a scroll
  // event, so afterwards isAtBottom would still (correctly) describe where
  // the reader was. Follow the tail only if they were at the bottom —
  // readers who scrolled up must not be yanked down (state machine, LIVE).
  const wasAtBottom = isAtBottom.value;
  await globalChatStore.pollForNewer();
  if ((messages.value?.length ?? 0) > before && wasAtBottom) {
    scrollToBottom();
  }
}

function handleGlobalChatNotification(notification: SignalRNotification) {
  if (notification.eventType !== NotificationType.NewGlobalChatMessage) return;
  fetchNewMessages();
}

let unsubscribeSignalR: (() => void) | null = null;

// Polling is demoted to a fallback transport: the tick is a no-op while the
// SignalR socket is delivering pushes and only performs fetches when the
// socket is down — and even then only while at the tail of the chat (avoids
// competing with archive/history browsing) and when the tab is visible.
const POLL_INTERVAL_MS = 30000;
let pollTimer: ReturnType<typeof setInterval> | null = null;

async function pollForNewMessages() {
  if (document.hidden) return;
  if (isSignalRConnected.value) return;
  await fetchNewMessages();
}

// Socket state transitions: catch up immediately instead of waiting for the
// next 30s tick — on disconnect (messages may arrive while the socket is
// down) and on (re)connect (messages may have been missed while it was down).
watch(isSignalRConnected, () => {
  fetchNewMessages();
});

function startPolling() {
  if (pollTimer) return;
  pollTimer = setInterval(pollForNewMessages, POLL_INTERVAL_MS);
}

function stopPolling() {
  if (pollTimer) {
    clearInterval(pollTimer);
    pollTimer = null;
  }
}

// ─────────────────────────────────────────────────────────────
// Message search (overlay panel opened from the events strip or Ctrl+F)
// ─────────────────────────────────────────────────────────────
const searchOpen = ref(false);

function openSearch() {
  searchOpen.value = true;
}

function closeSearch() {
  searchOpen.value = false;
}

// Ctrl+F (Cmd+F on macOS) opens the in-chat search instead of the browser find.
function handleSearchHotkey(event: KeyboardEvent) {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "f") {
    event.preventDefault();
    openSearch();
  }
}

// A global-chat search result jumps to the message in the live feed: leave
// any archive date, then load the window around it (highlightedMessageId
// watcher scrolls/flashes it).
function handleJumpToGlobalMessage(messageId: string) {
  searchOpen.value = false;
  selectedDate.value = "";
  if (route.query.date) {
    suppressNextDateWatch = true;
    router.replace({ name: "global-chat", query: {} });
  }
  globalChatStore.navigateToMessage(messageId);
}

onMounted(async () => {
  document.addEventListener("keydown", handleSearchHotkey);

  // Chat events (topbar banner + closed-event hint) — independent of the
  // message load, fire and forget.
  globalChatStore.fetchEvents();

  const hashMsgId = getHashMessageId();
  const dateQuery =
    typeof route.query.date === "string" ? route.query.date : null;

  if (hashMsgId) {
    suspendInfiniteScroll();
    await globalChatStore.navigateToMessage(hashMsgId);
    if (!messages.value?.length) {
      // Broken/stale #msg- link (message deleted or never existed) — the
      // store left messages empty rather than throwing, which would
      // otherwise render a false "Сообщений пока нет.". Fall back to the
      // normal latest-messages load and tell the guest why they landed here.
      await globalChatStore.fetchMessages();
      scrollToBottom();
      toast.error("Сообщение не найдено или удалено");
      nextTick(() => {
        setupInfiniteScroll();
        initBbcodeInteractive(messagesContainer.value);
        resumeInfiniteScroll();
      });
      return;
    }
    nextTick(() => {
      scrollToMessage(hashMsgId);
      setupInfiniteScroll();
      initBbcodeInteractive(messagesContainer.value);
      setTimeout(resumeInfiniteScroll, LANDING_SCROLL_MS);
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

  // Realtime: everyone — guests included — gets the SignalR push. The page
  // acquires its own lease on the shared connection (reused if App.vue
  // already connected it for an authenticated user, created anonymously
  // otherwise). Polling runs alongside purely as the fallback while the
  // socket is not connected — see pollForNewMessages.
  unsubscribeSignalR = onGlobalNotification(handleGlobalChatNotification);
  connectChatSignalR();
  startPolling();
  document.addEventListener("visibilitychange", pollForNewMessages);
});

onUnmounted(() => {
  document.removeEventListener("keydown", handleSearchHotkey);
  if (scrollEndTimeout) clearTimeout(scrollEndTimeout);
  stopPolling();
  unsubscribeSignalR?.();
  // Releases only this page's lease: closes the socket when the page was
  // its sole owner (guest), leaves the App.vue-owned one running otherwise.
  disconnectChatSignalR();
  document.removeEventListener("visibilitychange", pollForNewMessages);
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
    if (suppressNextDateWatch) {
      suppressNextDateWatch = false;
      return;
    }
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
  // Fetch the original BBCode from the backend — seeds ChatMessage's editor
  // once; further keystrokes stay inside ChatMessage's own local state.
  const { data } = await globalChatApi.getMessageForEdit(msg.id);
  editText.value = data?.text || "";
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

// Receives the edited text straight from ChatMessage's @save-edit payload
// (its own local editor state) — the page never reads back a stale copy.
async function saveEditWithText(msgId: string, text: string) {
  if (text.trim()) {
    const { error } = await globalChatStore.updateMessage(msgId, text);
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
async function toggleLike(msg: GlobalChatMessage) {
  if (!user.value) return;
  if (isLikedByMe(msg)) {
    await globalChatStore.unlikeMessage(msg.id);
  } else {
    await globalChatStore.likeMessage(msg.id);
  }
}

// Anchor
async function copyAnchor(msgId: string) {
  const url = `${window.location.origin}${window.location.pathname}#msg-${msgId}`;
  try {
    await navigator.clipboard.writeText(url);
    toast.success("Ссылка скопирована");
  } catch {
    toast.error("Не удалось скопировать ссылку");
  }
}

// --- Moderator warning (doc 4.2.4.1 / 4.2.2.4) ---
// Global-chat messages are public, so moderators (isModerator) get an
// "Оставить предупреждение" action in the hover toolbar. The dialog is
// prefilled with the message author and a #msg-{id} permalink; entityType
// "Message" lets the backend link the warning to the offending message.
const warnUsername = ref("");
const warnEntityId = ref<string | undefined>(undefined);
const warnEntityLink = ref<string | undefined>(undefined);

const { open: openWarnDialog, close: closeWarnDialog } = useModal({
  component: WarningDialog,
  attrs: reactive({
    username: warnUsername,
    entityId: warnEntityId,
    entityType: "Message",
    entityLink: warnEntityLink,
    onSuccess: () => closeWarnDialog(),
    onCancel: () => closeWarnDialog(),
  }),
});

function handleWarn(msg: GlobalChatMessage) {
  const username = msg.author?.username;
  if (!username) return;
  warnUsername.value = username;
  warnEntityId.value = msg.id;
  warnEntityLink.value = `${window.location.origin}${window.location.pathname}#msg-${msg.id}`;
  openWarnDialog();
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

// Load messages for an archive date and land on the first message of that
// LOCAL day. The URL (?date=YYYY-MM-DD) is the single source of truth —
// callers change the route, the route.query.date watcher routes here.
async function loadArchiveDate(date: string) {
  selectedDate.value = date;
  await globalChatStore.navigateToDate(date);

  // The store falls back to the latest-messages window when nothing is
  // found near the requested date (or the request fails) — that window is
  // NOT the picked day. Presenting it while the date picker still shows the
  // picked date as selected would look exactly like "landed on the wrong
  // day", so detect the fallback explicitly: sync the URL/UI back to
  // "latest" (messages are already loaded by the store's own fallback —
  // no second fetch needed) and tell the guest why they landed here.
  if (!landedOnRequestedDate.value) {
    selectedDate.value = "";
    if (route.query.date) {
      suppressNextDateWatch = true;
      router.replace({ name: "global-chat", query: {} });
    }
    if (messages.value?.length) {
      toast.error("Сообщений за эту дату не найдено — показаны последние");
      scrollToBottom();
    }
    return;
  }

  if (!messages.value?.length) return;
  // The store anchors the API request from local midnight, but the result
  // window can still start slightly before the requested day (nearest-cursor
  // semantics) — resolve the exact first message of the picked day client
  // side and scroll/highlight that one instead of blindly using index 0.
  const firstOfDay = messages.value.find(
    (m) => dayjs(m.createdUtc).format("YYYY-MM-DD") === date,
  );
  const targetId = firstOfDay?.id ?? messages.value[0].id;
  const index = messagesWithSeparators.value.findIndex(
    (item) => !isDateSeparator(item) && (item as any).id === targetId,
  );
  nextTick(() => {
    if (index >= 0) {
      scrollToIndex(index, { align: "start" });
    }
    setTimeout(() => {
      const element = document.getElementById(`msg-${targetId}`);
      if (element) {
        element.classList.add("highlighted");
        setTimeout(() => {
          element.classList.remove("highlighted");
        }, 1500);
      }
    }, 100);
  });
}

// Jump to latest. Clearing the date query lets the watcher load latest;
// when no date query is present, load directly. In LIVE mode with the tail
// already loaded (hasMoreAfter=false) there is nothing to fetch — just
// scroll (state machine: the button then only means "back to the bottom").
async function jumpToLatest() {
  selectedDate.value = "";
  if (route.query.date) {
    router.replace({ name: "global-chat", query: {} });
    return;
  }
  if (!hasMoreAfter.value) {
    scrollToBottom();
    return;
  }
  await globalChatStore.jumpToLatest();
  scrollToBottom();
}

// Scrolls to the last item through the virtualizer's own index-based API
// instead of a raw scrollTop=scrollHeight assignment — the virtualizer
// doesn't always have every row measured yet, so a DOM scrollHeight read can
// undershoot; scrollToIndex(..., {align:"end"}) lets it settle correctly.
function scrollToBottom() {
  nextTick(() => {
    const lastIndex = messagesWithSeparators.value.length - 1;
    if (lastIndex >= 0) {
      scrollToIndex(lastIndex, { align: "end" });
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
  const { error } = await globalChatStore.sendMessage(text);
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
  const { error } = await globalChatStore.deleteMessage(
    confirmingDeleteId.value,
  );
  confirmingDeleteId.value = null;
  if (error) notifyFailure(error, "Не удалось удалить сообщение");
}
</script>

<template>
  <page-title v-once>Глобальный чат</page-title>

  <div
    ref="globalChatContainer"
    class="globalChat-container"
    :class="{ 'layout-compact': isCompactLayout }"
  >
    <!-- Events panel pinned inside the chat frame, above the scrolling
         feed: the live-event row, the arrow-flipped upcoming event and the
         compact "К дате" calendar control in one block. -->
    <ChatEventsPanel
      :selected-date="selectedDate"
      :max-date="todayValue"
      @date-picked="onDatePicked"
      @open-search="openSearch"
    />

    <!-- Search overlay: layered over the feed, which stays mounted beneath. -->
    <MessageSearchPanel
      v-if="searchOpen"
      @close="closeSearch"
      @jump-global="handleJumpToGlobalMessage"
    />
    <div
      ref="messagesContainer"
      class="globalChat-messages"
      role="log"
      aria-label="Сообщения чата"
      :class="{
        'is-scrolling': isScrolling,
        'is-empty': !loading && !messages?.length,
      }"
      @scroll="handleScroll"
      @wheel.passive="handleWheel"
    >
      <ChatMessageSkeleton v-if="loading" :count="6" />
      <!-- Error takes precedence over fake-empty; stale content (if any) is preserved -->
      <div
        v-else-if="error && !messages?.length"
        class="globalChat-empty globalChat-error"
        role="alert"
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
        >
          <secondary-text v-if="errorBefore" class="sentinel-error">
            {{ errorBefore }}
            <button
              type="button"
              class="globalChat-retry sentinel-retry"
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
            <DashSeparator
              v-if="isDateSeparator(messagesWithSeparators[vRow.index])"
              spacing="tiny"
              :label="(messagesWithSeparators[vRow.index] as any).formattedDate"
              class="date-separator"
            />

            <div
              v-else
              :id="`msg-${(messagesWithSeparators[vRow.index] as any).id}`"
              :data-id="(messagesWithSeparators[vRow.index] as any).id"
              class="globalChat-message"
              tabindex="0"
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
              @focusin="
                handleMessageFocusIn(
                  $event,
                  (messagesWithSeparators[vRow.index] as any).id,
                )
              "
              @focusout="handleMessageFocusOut"
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
                  (text) =>
                    saveEditWithText(
                      (messagesWithSeparators[vRow.index] as any).id,
                      text,
                    )
                "
                @cancel-edit="cancelEdit"
              />
            </div>
          </div>
        </div>

        <!-- Bottom sentinel for loading newer messages -->
        <div
          v-if="hasMoreAfter"
          ref="bottomSentinel"
          class="scroll-sentinel bottom-sentinel"
        >
          <secondary-text v-if="errorAfter" class="sentinel-error">
            {{ errorAfter }}
            <button
              type="button"
              class="globalChat-retry sentinel-retry"
              @click="loadNewer()"
            >
              Повторить
            </button>
          </secondary-text>
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
      ref="toolbarEl"
      class="msg-toolbar"
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
            <SvgIcon
              :name="isLikedByMe(hoveredMessage) ? 'heartFilled' : 'heartEmpty'"
            />
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
        <Tooltip v-if="isModerator" text="Оставить предупреждение">
          <button
            class="toolbar-btn toolbar-btn-warn"
            aria-label="Оставить предупреждение"
            @click="handleWarn(hoveredMessage)"
          >
            {{ symbols.warning }}
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

    <!-- Scroll to latest button (centered over globalChat). Shown when
         there is somewhere to jump (state machine above): newer pages
         exist to fetch (hasMoreAfter), or the reader scrolled DEEP up from
         the bottom of the loaded window (isFarFromBottom hysteresis) — in
         ARCHIVE and LIVE mode alike. Near the bottom of the live tail it
         has nothing to offer and hides. -->
    <button
      v-if="messages?.length && (hasMoreAfter || isFarFromBottom)"
      class="scroll-to-latest"
      aria-label="К последним сообщениям"
      @click="jumpToLatest"
    >
      <SvgIcon name="chevronDown" />
    </button>
  </div>

  <!-- Input area below globalChat window -->
  <div class="globalChat-input-wrapper">
    <!-- Quiet notice for non-participants while a closed event is live:
         the backend rejects their messages, so warn before they type. -->
    <secondary-text v-if="showClosedEventHint" class="globalChat-event-hint">
      Идет закрытый эвент — писать могут только участники
    </secondary-text>
    <div class="globalChat-input-container">
      <template v-if="canSendMessages">
        <BBCodeEditor
          ref="editorRef"
          v-model="newMessage"
          context="message"
          placeholder=""
          :draft-key="composerDraftKey('global-chat', 'message')"
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

      <LoginPrompt v-else action="отправлять сообщения" />
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/BbcodeContent"
@import "@/assets/styles/Inputs"
@import "@/assets/styles/ZIndex"

.globalChat-container
  display: flex
  flex-direction: column
  min-height: 400px
  position: relative
  border: 1px dashed
  border-color: $border
  overflow: hidden  // Clips toolbar when outside bounds

  // Compact display — wrapper overrides (own element, no :deep needed).
  // Horizontal padding matches the full layout ($medium) so the compact
  // content column ($compact-time-gutter + gap in ChatMessage) lands on the
  // same x as the full layout's content (avatar + gap).
  &.layout-compact
    .globalChat-message
      padding: $tiny $medium
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
  justify-content: flex-start
  height: 100%
  padding: $big

.globalChat-error
  flex-direction: column
  gap: $small

.globalChat-retry
  +button

.scroll-sentinel
  width: 100%

.top-sentinel,
.bottom-sentinel
  display: flex
  justify-content: center
  align-items: center
  min-height: 1px
  // Grows to fit the retry banner when a history-pagination request fails;
  // otherwise stays a hairline intersection target.
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

// DashSeparator owns its own flex/label layout — this override only adds
// the GPU-compositing hint needed inside the virtualized/transformed list.
.date-separator
  transform: translateZ(0)
  backface-visibility: hidden

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
  &.hovered,
  &:focus-visible
    background-color: $bg-element
    border-radius: 0 $border-radius $border-radius 0

  // tabindex="0" makes the whole row focusable so keyboard users can reach
  // the hover-only toolbar (focusin -> handleMessageFocusIn); outline only
  // on :focus-visible so mouse clicks don't leave a visible ring.
  &:focus
    outline: none
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: -2px



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

// Warn action renders the ⚠ text glyph (no dedicated SVG icon) — size it to
// match the SVG icons' visual weight and keep it monochrome via inherited
// color (same treatment as StatusIcon's warning symbol).
.toolbar-btn-warn
  font-size: 18px
  line-height: 1

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

  // Liked: filled heart (bound in template) tinted with the shared
  // accent-red like token so the toolbar matches the forum and chat badges.
  &.active
    color: $accent-red
    &:hover
      background-color: $bg-element-accent
      svg
        filter: brightness($hover-brightness)

// BBCodeEditor overlay on message hover
// The overlay is layered ON TOP of the base $input-bg (does not replace it)
.globalChat-message:hover :deep(.bbcode-editor),
.globalChat-message.hovered :deep(.bbcode-editor)
  background: linear-gradient($hover-overlay, $hover-overlay), $input-bg

.globalChat-input-wrapper
  margin-top: $medium

// Quiet single-line notice above the editor (mirrors the guest CTA styling)
.globalChat-event-hint
  display: block
  padding-bottom: $tiny

// CHAT-10: same control idiom as the fixed scroll-nav buttons (24px square,
// 16px glyph, $border-radius) — the 40px square read as oversized. Keeps a
// soft shadow because it floats over message content.
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
</style>
