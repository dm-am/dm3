<script setup lang="ts">
/**
 * ChatFeed: the feed of a chat, the global chat and a private correspondence
 * alike.
 *
 * What makes a feed is here, once: the dashed frame; the line at its top (the
 * `strip` slot: the events of the global chat, who a correspondence is with);
 * the line under it explaining a window the reader did not ask for; the list
 * with its date separators, virtualized; the toolbar over a message; the button
 * back to the latest messages; the skeleton, the failure with its retry and the
 * empty feed; and the composer under the frame, drawn only for a reader who may
 * write (`canWrite`), with the `closed` slot standing in for it otherwise (the
 * guest's invitation to sign in).
 *
 * What differs is what the page hands in: the messages and the store's answers
 * (`actions`), the words of the empty feed and of the list's name, where a link
 * to a message leads (`linkTo`), whom the reader may warn (`canWarn`), and what
 * happens on the way back to the latest messages (the global chat leaves its
 * archive date). The page drives the window: it loads, it lands on a message
 * (`scrollToMessage`), it follows the tail (`scrollToBottom`, `atBottom`).
 */
import { computed, nextTick, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { useAuthStore, useMessagePermissions } from "@/entities/user";
import { ChatMessage } from "@/widgets/chat-message/@x/chat-feed";
import { useUiStore } from "@/shared/stores/ui";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import { ActionResult } from "@/shared/ui/ActionResult";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ChatMessageSkeleton } from "@/shared/ui/Skeleton";
import { symbols } from "@/shared/lib/utils/icons";
import type { BBCodeContext } from "@/shared/lib/utils/bbcode";
import { initBbcodeInteractive } from "@/shared/lib/utils/bbcodeInteractive";
import { isNearBottom } from "@/shared/lib/utils/liveTail";
import {
  groupMessagesWithSeparators,
  isDateSeparator,
  isUserOnline,
  toChatVirtualRows,
  type MessageOrSeparator,
} from "@/shared/lib/utils/chat";
import type { Message } from "@/shared/api/models/common";
import type { RouteLocationRaw } from "vue-router";
import { useVirtualScroll } from "@/shared/lib/composables/useVirtualScroll";
import { useMessageToolbar } from "@/shared/lib/composables/useMessageToolbar";
import { useChatComposer } from "@/shared/lib/composables/useChatComposer";
import { useAnchoredInfiniteScroll } from "@/shared/lib/composables/useAnchoredInfiniteScroll";
import { markArrival } from "@/shared/lib/composables/useScrollToElement";
import ChatComposer from "./ChatComposer.vue";
import type { ChatFeedActions } from "./types";
import { MESSAGE_FOLD_HEIGHT } from "./types";

const props = withDefaults(
  defineProps<{
    /** The loaded window, oldest first. */
    messages: readonly Message[];
    /** The window is on its way and there is nothing to show yet. */
    loading: boolean;
    /** The window did not load; said with a retry where the feed would be. */
    error?: string | null;
    /** The retry of a window that did not load. */
    retry?: () => unknown;
    /** What an empty feed says. */
    emptyText: string;
    /** Why the feed shows a window the reader did not ask for. */
    notice?: string | null;
    /** The list's name for a reader of the screen. */
    label: string;
    /** The tag set of the composer and of the edit box. */
    context: BBCodeContext;
    hasMoreBefore: boolean;
    hasMoreAfter?: boolean;
    /** A page of older messages that did not load, at the top with a retry. */
    errorBefore?: string | null;
    /** A page of newer messages that did not load, at the bottom. */
    errorAfter?: string | null;
    loadOlder: () => Promise<unknown>;
    loadNewer?: () => Promise<unknown>;
    /** The store's answers to what the reader does in the feed. */
    actions: ChatFeedActions;
    /** A send is in flight: the store's own flag. */
    sending: boolean;
    /** The reader may write here; without it there is no composer at all. */
    canWrite: boolean;
    /** The composer waits for something besides a send. */
    composerDisabled?: boolean;
    /** Where the unsent text outlives the tab. */
    draftKey: string;
    /**
     * Where the link to a message leads: the page of the chat standing on
     * that message. An ordinary link, which the reader opens or copies the
     * way he copies any other.
     */
    linkTo: (id: string) => RouteLocationRaw;
    /** The reader may warn the author: a moderator in the global chat. */
    canWarn?: (message: Message) => boolean;
  }>(),
  {
    error: null,
    retry: undefined,
    notice: null,
    hasMoreAfter: false,
    errorBefore: null,
    errorAfter: null,
    loadNewer: undefined,
    composerDisabled: false,
    canWarn: () => false,
  },
);

const emit = defineEmits<{
  /** "К последним сообщениям" was pressed. */
  "jump-to-latest": [];
  /** The feed scrolled; the page may ask where the reader stands now. */
  scroll: [];
  /** "Оставить предупреждение" over a message. */
  warn: [message: Message];
}>();

const { user } = storeToRefs(useAuthStore());
const { isCompactLayout } = storeToRefs(useUiStore());
const { isModerator, canEdit, canDelete, canLike } =
  useMessagePermissions(user);

const frame = ref<HTMLElement | null>(null);
const list = ref<HTMLElement | null>(null);
const topSentinel = ref<HTMLElement | null>(null);
const bottomSentinel = ref<HTMLElement | null>(null);
const toolbarEl = ref<HTMLElement | null>(null);
const composer = ref<InstanceType<typeof ChatComposer> | null>(null);

// ─────────────────────────────────────────────────────────────
// The list
// ─────────────────────────────────────────────────────────────
const rows = computed((): MessageOrSeparator[] =>
  groupMessagesWithSeparators([...props.messages]),
);

// Stable per-item key (message id / date-separator date) instead of the
// virtualizer's default index-based key: prepending older history shifts every
// index, and measurements and the hover would slide onto another message.
function rowKey(index: number): string {
  const item = rows.value[index];
  if (!item) return String(index);
  return isDateSeparator(item) ? `sep-${item.date}` : item.id;
}

const { virtualItems, totalSize, measureElement, scrollToIndex } =
  useVirtualScroll({
    count: computed(() => rows.value.length),
    container: list,
    estimateSize: 80,
    overscan: 15,
    getItemKey: rowKey,
  });

const virtualRows = computed(() =>
  toChatVirtualRows(rows.value, virtualItems.value),
);

// New messages bring new markup. Only the length is watched: a deep watch
// walked every message on every like, and each message initialises its own
// markup already (TruncatedContent's on-content-mounted).
watch(
  () => props.messages.length,
  () => nextTick(initMarkup),
);

function initMarkup() {
  if (list.value) initBbcodeInteractive(list.value);
}

// ─────────────────────────────────────────────────────────────
// Where the reader stands
// ─────────────────────────────────────────────────────────────
const isScrolling = ref(false);
let scrollEndTimeout: ReturnType<typeof setTimeout> | null = null;

/**
 * The button back to the latest messages appears after a deep departure from
 * the bottom (a whole viewport up), not on the first turn of the wheel, and
 * goes once the reader is nearly back: the gap between the two is hysteresis,
 * so it does not flicker at either edge.
 */
const isFarFromBottom = ref(false);

function measureDeparture() {
  const el = list.value;
  if (!el) return;
  const distance = el.scrollHeight - el.scrollTop - el.clientHeight;
  if (distance > el.clientHeight) isFarFromBottom.value = true;
  else if (isNearBottom(el)) isFarFromBottom.value = false;
}

function settleScrolling() {
  if (scrollEndTimeout) clearTimeout(scrollEndTimeout);
  scrollEndTimeout = setTimeout(() => {
    isScrolling.value = false;
    scrollEndTimeout = null;
  }, 150);
}

function handleWheel() {
  isScrolling.value = true;
  handleScrollStart();
  settleScrolling();
}

function handleScroll() {
  isScrolling.value = true;
  if (hoveredMessageId.value) {
    hoveredMessageId.value = null;
    confirmingDeleteId.value = null;
    isToolbarHovered.value = false;
  }
  measureDeparture();
  emit("scroll");
  settleScrolling();
}

/** The reader stands at the end of the loaded window, measured now. */
function atBottom(): boolean {
  return list.value ? isNearBottom(list.value) : true;
}

// Scrolls to the last item through the virtualizer rather than scrollTop =
// scrollHeight: rows not measured yet make scrollHeight undershoot.
function scrollToBottom() {
  nextTick(() => {
    const last = rows.value.length - 1;
    if (last >= 0) scrollToIndex(last, { align: "end" });
  });
}

/**
 * Brings a message into view and marks its arrival: the site's pulse for a
 * place one was taken to (.highlight-unread, two seconds of yellow).
 */
function scrollToMessage(id: string, align: "center" | "start" = "center") {
  const index = rows.value.findIndex(
    (item) => !isDateSeparator(item) && item.id === id,
  );
  if (index >= 0) scrollToIndex(index, { align });
  // The virtualizer draws the row after it has scrolled to it.
  nextTick(() => {
    setTimeout(() => {
      const element = document.getElementById(`msg-${id}`);
      if (element) markArrival(element);
    }, 100);
  });
}

// ─────────────────────────────────────────────────────────────
// Paging, both ends
// ─────────────────────────────────────────────────────────────
const {
  loadOlder,
  loadNewer,
  setupInfiniteScroll,
  suspend: suspendInfiniteScroll,
  resume: resumeInfiniteScroll,
} = useAnchoredInfiniteScroll({
  container: list,
  older: {
    sentinel: topSentinel,
    hasMore: () => props.hasMoreBefore,
    load: () => props.loadOlder(),
    failed: () => Boolean(props.errorBefore),
  },
  newer: {
    sentinel: bottomSentinel,
    hasMore: () => props.hasMoreAfter && Boolean(props.loadNewer),
    load: () => props.loadNewer?.() ?? Promise.resolve(),
    failed: () => Boolean(props.errorAfter),
  },
});

// ─────────────────────────────────────────────────────────────
// The toolbar over a message
// ─────────────────────────────────────────────────────────────
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
  container: frame,
  toolbar: toolbarEl,
  isScrolling: () => isScrolling.value,
});

const hoveredMessage = computed(() =>
  hoveredMessageId.value
    ? (props.messages.find((m) => m.id === hoveredMessageId.value) ?? null)
    : null,
);

// ─────────────────────────────────────────────────────────────
// Writing, deleting, the line under a refused press
// ─────────────────────────────────────────────────────────────
const {
  newMessage,
  handleSend,
  requestDelete,
  cancelDelete,
  confirmDelete,
  sendResult,
  messageRefusal,
  refuseMessage,
  clearMessageRefusal,
} = useChatComposer({
  sending: computed(() => props.sending),
  send: (text) => props.actions.send(text),
  remove: (id) => props.actions.remove(id),
  editor: composer,
  confirmingDeleteId,
  scrollToBottom,
  canSend: () => props.canWrite && !props.composerDisabled,
});

// ─────────────────────────────────────────────────────────────
// A message: who is online, likes, the edit, the removed one
// ─────────────────────────────────────────────────────────────
// The latest activity of every author in the window: a name is green while its
// author was active lately. ISO strings compare in time order.
const latestActivity = computed(() => {
  const map = new Map<string, string>();
  for (const msg of props.messages) {
    const name = msg.author?.username;
    const seen = msg.author?.lastActivityUtc;
    if (!name || !seen) continue;
    const known = map.get(name);
    if (!known || seen > known) map.set(name, seen);
  }
  return map;
});

function isOnline(message: Message): boolean {
  const name = message.author?.username;
  return name ? isUserOnline(latestActivity.value.get(name)) : false;
}

function isLikedByMe(message: Message): boolean {
  const name = user.value?.username;
  if (!name) return false;
  return message.likes?.some((u) => u.username === name) ?? false;
}

async function toggleLike(message: Message) {
  if (!user.value) return;
  // A refused like changes nothing on the heart, so the line under the message
  // says it, until the next press on the heart.
  clearMessageRefusal(message.id);
  const unliking = isLikedByMe(message);
  const { error } = unliking
    ? await props.actions.unlike(message.id)
    : await props.actions.like(message.id);
  if (error)
    refuseMessage(
      message.id,
      error,
      unliking ? "Не удалось убрать лайк" : "Не удалось поставить лайк",
    );
}

// The edit box is seeded once with the message's own BBCode, asked of the
// server; what is typed afterwards lives in ChatMessage and comes back as the
// payload of save-edit.
const editingId = ref<string | null>(null);
const editText = ref("");

async function startEdit(message: Message) {
  editingId.value = message.id;
  editText.value = await props.actions.editText(message.id);
}

function cancelEdit() {
  editingId.value = null;
  editText.value = "";
  nextTick(initMarkup);
}

async function saveEdit(id: string, text: string) {
  clearMessageRefusal(id);
  if (text.trim()) {
    const { error } = await props.actions.saveEdit(id, text);
    // The editor stays open with the text in it: a closed editor over the
    // unchanged message would say the edit went through.
    if (error) {
      refuseMessage(id, error, "Не удалось сохранить сообщение");
      return;
    }
  }
  cancelEdit();
}

// A removed message opens for a moderator, a deliberate act and not a fold:
// "Развернуть все" never uncovers what was removed.
const expandedRemoved = ref<Set<string>>(new Set());

function toggleRemoved(id: string) {
  if (!isModerator.value) return;
  if (expandedRemoved.value.has(id)) expandedRemoved.value.delete(id);
  else expandedRemoved.value.add(id);
}

defineExpose({
  scrollToBottom,
  scrollToMessage,
  atBottom,
  setupInfiniteScroll,
  suspendInfiniteScroll,
  resumeInfiniteScroll,
});
</script>

<template>
  <div class="chat-feed">
    <div
      ref="frame"
      class="chat-feed__frame"
      :class="{ 'layout-compact': isCompactLayout }"
    >
      <slot name="strip" />
      <!-- Why the feed shows its tail instead of the place the reader asked
           for, for as long as that window stands. -->
      <p v-if="notice" class="chat-feed__notice" role="status">
        {{ notice }}
      </p>
      <div
        ref="list"
        class="chat-feed__list"
        role="log"
        :aria-label="label"
        :class="{
          'is-scrolling': isScrolling,
          'is-empty': !loading && !messages.length,
        }"
        @scroll="handleScroll"
        @wheel.passive="handleWheel"
      >
        <ChatMessageSkeleton v-if="loading" :count="6" />
        <!-- A failure is not an empty feed. What the retry shows is the
             skeleton: the store raises loading before it awaits. -->
        <ErrorState
          v-else-if="error && !messages.length"
          :message="error"
          :retry="retry"
        />
        <secondary-text v-else-if="!messages.length" class="chat-feed__empty">
          {{ emptyText }}
        </secondary-text>
        <template v-else>
          <div
            v-if="hasMoreBefore"
            ref="topSentinel"
            class="chat-feed__sentinel"
          >
            <secondary-text
              v-if="errorBefore"
              class="chat-feed__sentinel-error"
            >
              {{ errorBefore }}
              <button
                type="button"
                class="chat-feed__retry"
                @click="loadOlder()"
              >
                Повторить
              </button>
            </secondary-text>
          </div>

          <div
            :style="{
              height: `${totalSize}px`,
              width: '100%',
              position: 'relative',
            }"
          >
            <div
              v-for="row in virtualRows"
              :key="row.key"
              :ref="
                (el) => {
                  if (el) measureElement(el as HTMLElement);
                }
              "
              :data-index="row.index"
              class="chat-feed__row"
              :class="{
                'chat-feed__row--start': row.index === 0 && !hasMoreBefore,
              }"
              :style="{ top: `${row.start}px` }"
            >
              <DashSeparator
                v-if="row.kind === 'separator'"
                spacing="tiny"
                :label="row.separator.formattedDate"
                class="chat-feed__date"
              />
              <div
                v-else
                :id="`msg-${row.message.id}`"
                :data-id="row.message.id"
                class="chat-feed__message"
                tabindex="0"
                :class="{
                  removed: row.message.isRemoved,
                  hovered: hoveredMessageId === row.message.id,
                  continuation: row.message.isContinuation,
                  'removed-collapsed':
                    row.message.isRemoved &&
                    !expandedRemoved.has(row.message.id),
                }"
                @mouseenter="handleMessageMouseEnter($event, row.message.id)"
                @mouseleave="handleMessageMouseLeave"
                @focusin="handleMessageFocusIn($event, row.message.id)"
                @focusout="handleMessageFocusOut"
              >
                <ChatMessage
                  :message="row.message"
                  :context="context"
                  :compact="isCompactLayout"
                  :hovered="hoveredMessageId === row.message.id"
                  :is-online="isOnline(row.message)"
                  :is-liked-by-me="isLikedByMe(row.message)"
                  :can-edit="canEdit(row.message)"
                  :can-delete="canDelete(row.message)"
                  :can-like="canLike(row.message)"
                  :is-moderator="isModerator"
                  :is-editing="editingId === row.message.id"
                  :edit-text="editText"
                  :is-deleted-expanded="expandedRemoved.has(row.message.id)"
                  :max-height="MESSAGE_FOLD_HEIGHT"
                  @like="toggleLike(row.message)"
                  @toggle-deleted="toggleRemoved(row.message.id)"
                  @start-edit="startEdit(row.message)"
                  @save-edit="(text) => saveEdit(row.message.id, text)"
                  @cancel-edit="cancelEdit"
                />
                <ActionResult
                  v-if="messageRefusal?.id === row.message.id"
                  :text="messageRefusal.text"
                />
              </div>
            </div>
          </div>

          <div
            v-if="hasMoreAfter"
            ref="bottomSentinel"
            class="chat-feed__sentinel"
          >
            <secondary-text v-if="errorAfter" class="chat-feed__sentinel-error">
              {{ errorAfter }}
              <button
                type="button"
                class="chat-feed__retry"
                @click="loadNewer()"
              >
                Повторить
              </button>
            </secondary-text>
          </div>
        </template>
      </div>

      <!-- The toolbar stands inside the frame, which clips it. -->
      <div
        v-if="
          hoveredMessage &&
          !hoveredMessage.isRemoved &&
          editingId !== hoveredMessage.id
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
        <template v-if="confirmingDeleteId === hoveredMessage.id">
          <Tooltip text="Подтвердить удаление">
            <button
              type="button"
              class="toolbar-btn toolbar-btn-delete-confirm"
              aria-label="Подтвердить удаление"
              @click="confirmDelete"
            >
              <SvgIcon name="trash" />
            </button>
          </Tooltip>
          <Tooltip text="Отмена">
            <button
              type="button"
              class="toolbar-btn toolbar-btn-cancel"
              aria-label="Отмена"
              @click="cancelDelete"
            >
              {{ symbols.close }}
            </button>
          </Tooltip>
        </template>
        <template v-else>
          <Tooltip
            v-if="canLike(hoveredMessage)"
            :text="
              isLikedByMe(hoveredMessage) ? 'Убрать лайк' : 'Поставить лайк'
            "
          >
            <button
              type="button"
              class="toolbar-btn"
              :class="{ active: isLikedByMe(hoveredMessage) }"
              :aria-label="
                isLikedByMe(hoveredMessage) ? 'Убрать лайк' : 'Поставить лайк'
              "
              @click="toggleLike(hoveredMessage)"
            >
              <SvgIcon
                :name="
                  isLikedByMe(hoveredMessage) ? 'heartFilled' : 'heartEmpty'
                "
              />
            </button>
          </Tooltip>
          <Tooltip v-if="canEdit(hoveredMessage)" text="Редактировать">
            <button
              type="button"
              class="toolbar-btn"
              aria-label="Редактировать"
              @click="startEdit(hoveredMessage)"
            >
              <SvgIcon name="pencil" />
            </button>
          </Tooltip>
          <Tooltip v-if="canDelete(hoveredMessage)" text="Удалить">
            <button
              type="button"
              class="toolbar-btn"
              aria-label="Удалить"
              @click="requestDelete(hoveredMessage.id)"
            >
              <SvgIcon name="trash" />
            </button>
          </Tooltip>
          <Tooltip
            v-if="canWarn(hoveredMessage)"
            text="Оставить предупреждение"
          >
            <button
              type="button"
              class="toolbar-btn toolbar-btn-warn"
              aria-label="Оставить предупреждение"
              @click="emit('warn', hoveredMessage)"
            >
              {{ symbols.warning }}
            </button>
          </Tooltip>
          <!-- An ordinary link to the message, opened or copied as any other
               link of the site. It is not the current page, so it never says
               it is. -->
          <Tooltip text="Ссылка на сообщение">
            <router-link
              class="toolbar-btn"
              :to="linkTo(hoveredMessage.id)"
              aria-label="Ссылка на сообщение"
              :aria-current="undefined"
            >
              <SvgIcon name="anchor" />
            </router-link>
          </Tooltip>
        </template>
      </div>

      <!-- Back to the latest messages: while newer pages are there to fetch,
           or once the reader went a whole viewport up. -->
      <button
        v-if="messages.length && (hasMoreAfter || isFarFromBottom)"
        type="button"
        class="chat-feed__latest"
        aria-label="К последним сообщениям"
        @click="emit('jump-to-latest')"
      >
        <SvgIcon name="chevronDown" />
      </button>
    </div>

    <div class="chat-feed__composer">
      <ChatComposer
        v-if="canWrite"
        ref="composer"
        v-model="newMessage"
        :context="context"
        :draft-key="draftKey"
        :disabled="sending || composerDisabled"
        :is-moderator="isModerator"
        :result="sendResult"
        @submit="handleSend"
      />
      <slot v-else name="closed" />
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/MessageToolbar" as *
@use "@/assets/styles/ZIndex" as *

// The hover toolbar over a message, from its one partial.
+message-toolbar

.chat-feed
  display: flex
  flex-direction: column
  min-width: 0

// The frame clips the toolbar where it would stand outside it.
.chat-feed__frame
  display: flex
  flex-direction: column
  min-height: 400px
  position: relative
  border: 1px dashed $border
  overflow: hidden

  // Compact display: the full layout's side padding, so the compact content
  // column lands on the x of the full layout's text.
  &.layout-compact
    .chat-feed__message
      padding: $tiny $medium
      margin-bottom: $small
      &.removed-collapsed
        margin-bottom: $tiny
      &.continuation
        margin-top: 0

// The line that explains a fallback window, in the strip's own dress. It wraps
// where the strip keeps one line: it has no title to protect, and every word
// of it is the point.
.chat-feed__notice
  flex-shrink: 0
  margin: 0
  padding: $small $medium
  font-size: $secondary-font-size
  line-height: 1.4
  color: $text-muted
  background-color: $bg-element
  border-bottom: 1px dashed $border

// No padding of its own, so the highlight of a message runs from edge to edge
// of the frame; room for the scrollbar only when there is something to scroll.
// No containment and no transform here or on a row: either makes the box the
// containing block of every fixed layer inside it, and the menu of a message's
// edit box is such a layer. It opened inside the clipped feed instead of over
// the page.
.chat-feed__list
  // Everything the page spends around the feed, the search row above the
  // frame included, written as the token so the feed follows it.
  height: calc(100vh - 362px - #{$control-height})
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
  // Hover effects wait for the scroll to end.
  &.is-scrolling .chat-feed__message
    pointer-events: none
  &.is-empty
    overflow: hidden

.chat-feed__empty
  display: flex
  align-items: center
  justify-content: flex-start
  height: 100%
  padding: $big

.chat-feed__sentinel
  display: flex
  justify-content: center
  align-items: center
  width: 100%
  min-height: 1px
  // Grows to fit the retry when a page of history did not load; otherwise a
  // hairline the observer watches.
  &:has(.chat-feed__sentinel-error)
    min-height: 30px
    padding: $small 0

.chat-feed__sentinel-error
  display: flex
  align-items: center
  gap: $small
  color: $accent-red

.chat-feed__retry
  flex-shrink: 0
  +button

.chat-feed__row
  position: absolute
  top: 0
  left: 0
  width: 100%

// The very start of the history: the first date does not touch the dashed
// line of the strip above it. Part of the row, so the virtualizer measures it.
.chat-feed__row--start
  padding-top: $small

.chat-feed__message
  padding: $small $medium
  margin-bottom: $medium
  word-break: break-word
  overflow-wrap: break-word
  position: relative
  overflow: visible
  background-color: $bg-page

  &.continuation
    margin-bottom: $tiny
    margin-top: -$small

  // A message is content, and its highlight is square like every block of
  // content (owner's standard, 27 September 2026).
  &:hover,
  &.hovered,
  &:focus-visible
    background-color: $bg-element

  // The whole row takes the focus, so the keyboard reaches the toolbar; the
  // ring goes inward, since messages stand flush.
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    outline: 2px solid $link
    outline-offset: -2px

  &.removed
    color: $text-muted

// The overlay is laid over the editor's own fill, not in place of it.
.chat-feed__message:hover :deep(.bbcode-editor),
.chat-feed__message.hovered :deep(.bbcode-editor)
  background: linear-gradient($hover-overlay, $hover-overlay), $input-bg

// Same control idiom as the fixed scroll-nav buttons (24px square, 16px
// glyph), with a soft shadow because it floats over the messages.
.chat-feed__latest
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

// Under the frame, a step away from it.
.chat-feed__composer
  margin-top: $medium
</style>
