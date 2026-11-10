<script setup lang="ts">
// The feed of both chats: what makes a feed is here once, what differs the page hands in.
import { computed, nextTick, ref, watch, type Ref } from "vue";
import { storeToRefs } from "pinia";
import { useAuthStore, useMessagePermissions } from "@/entities/user";
import { ChatMessage } from "@/widgets/chat-message/@x/chat-feed";
import { useUiStore } from "@/shared/stores/ui";
import { ActionResult } from "@/shared/ui/ActionResult";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ChatMessageSkeleton } from "@/shared/ui/Skeleton";
import type { BBCodeContext } from "@/shared/lib/bbcode/bbcodeTagSet";
import { initBbcodeInteractive } from "@/shared/lib/bbcode/bbcodeInteractive";
import {
  groupMessagesWithSeparators,
  isDateSeparator,
  isUserOnline,
  latestActivityByUsername,
  toChatVirtualRows,
  type MessageOrSeparator,
} from "@/shared/lib/utils/chat";
import type { Message } from "@/shared/api/models/common";
import type { RouteLocationRaw } from "vue-router";
import { useVirtualScroll } from "@/shared/lib/composables/useVirtualScroll";
import { useMessageToolbar } from "@/shared/lib/composables/useMessageToolbar";
import { useChatComposer } from "@/shared/lib/composables/useChatComposer";
import { useAnchoredInfiniteScroll } from "@/shared/lib/composables/useAnchoredInfiniteScroll";
import ChatComposer from "./ChatComposer.vue";
import ChatStrip from "./ChatStrip.vue";
import FeedEdge from "./FeedEdge.vue";
import JumpToLatest from "./JumpToLatest.vue";
import MessageToolbar from "./MessageToolbar.vue";
import { useFeedPosition } from "./useFeedPosition";
import { useMessageEdit } from "./useMessageEdit";
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
    retry: () => unknown;
    /** What an empty feed says. */
    emptyText: string;
    /** Why the feed shows a window the reader did not ask for. */
    notice?: string | null;
    /** The list's name for a reader of the screen. */
    label: string;
    /** The tag set of the composer and of the edit box. */
    context: BBCodeContext;
    hasMoreBefore: boolean;
    hasMoreAfter: boolean;
    /** A page of older messages that did not load, at the top with a retry. */
    errorBefore?: string | null;
    /** A page of newer messages that did not load, at the bottom. */
    errorAfter?: string | null;
    loadOlder: () => Promise<unknown>;
    loadNewer: () => Promise<unknown>;
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
    /** Where the link to a message leads: the page of the chat standing on it. */
    linkTo: (id: string) => RouteLocationRaw;
    /** Moderation reaches this chat, the global one alone; the page says whom the reader may warn. */
    moderation?: { canWarn: (message: Message) => boolean };
  }>(),
  {
    error: null,
    notice: null,
    errorBefore: null,
    errorAfter: null,
    composerDisabled: false,
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
const permissions = useMessagePermissions(user, props);
const { isModerator, canEdit, canDelete, canLike } = permissions;

const frame = ref<HTMLElement | null>(null);
const list = ref<HTMLElement | null>(null);
const topEdge = ref<InstanceType<typeof FeedEdge> | null>(null);
const bottomEdge = ref<InstanceType<typeof FeedEdge> | null>(null);
const toolbar = ref<InstanceType<typeof MessageToolbar> | null>(null);
const composer = ref<InstanceType<typeof ChatComposer> | null>(null);
// The roots of the parts: what the observers watch and the toolbar's focus is told by.
const rootOf = (part: Ref<{ $el: HTMLElement } | null>) =>
  computed<HTMLElement | null>(() => part.value?.$el ?? null);
const topSentinel = rootOf(topEdge);
const bottomSentinel = rootOf(bottomEdge);
const toolbarEl = rootOf(toolbar);

const rows = computed((): MessageOrSeparator[] =>
  groupMessagesWithSeparators([...props.messages]),
);

// A key per item, not per index: prepended older history shifts every index.
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

// Only the length: a deep watch walked every message on every like.
watch(
  () => props.messages.length,
  () => nextTick(initMarkup),
);

function initMarkup() {
  if (list.value) initBbcodeInteractive(list.value);
}

const {
  isScrolling,
  isFarFromBottom,
  measureDeparture,
  settleScrolling,
  atBottom,
  scrollToBottom,
  scrollToMessage,
} = useFeedPosition({ list, rows, scrollToIndex });

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
    hasMore: () => props.hasMoreAfter,
    load: () => props.loadNewer(),
    failed: () => Boolean(props.errorAfter),
  },
});

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

const latestActivity = computed(() => latestActivityByUsername(props.messages));

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
  // A refused like changes nothing on "Нравится", so the line under the message says it.
  clearMessageRefusal(message.id);
  const unliking = isLikedByMe(message);
  const { error } = unliking
    ? await props.actions.unlike(message.id)
    : await props.actions.like(message.id);
  const failure = unliking
    ? "Не удалось убрать отметку"
    : "Не удалось отметить";
  if (error) refuseMessage(message.id, error, failure);
}

const { editingId, editText, startEdit, cancelEdit } = useMessageEdit({
  load: (id) => props.actions.editText(id),
  closed: () => nextTick(initMarkup),
});

async function saveEdit(id: string, text: string) {
  clearMessageRefusal(id);
  if (text.trim()) {
    const { error } = await props.actions.saveEdit(id, text);
    // Closed over the unchanged message, the editor would say the edit went through.
    if (error) {
      refuseMessage(id, error, "Не удалось сохранить сообщение");
      return;
    }
  }
  cancelEdit();
}

// A moderator's deliberate act, not a fold: "Развернуть все" never uncovers it.
const expandedRemoved = ref<Set<string>>(new Set());

function toggleRemoved(id: string) {
  if (!permissions.revealsRemoved.value) return;
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
      <!-- Why the feed shows its tail instead of the place asked for -->
      <ChatStrip v-if="notice" class="chat-feed__notice" role="status">
        {{ notice }}
      </ChatStrip>
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
        <!-- A failure is not an empty feed -->
        <ErrorState
          v-else-if="error && !messages.length"
          :message="error"
          :retry="retry"
        />
        <secondary-text v-else-if="!messages.length" class="chat-feed__empty">
          {{ emptyText }}
        </secondary-text>
        <template v-else>
          <FeedEdge
            v-if="hasMoreBefore"
            ref="topEdge"
            :error="errorBefore"
            @retry="loadOlder()"
          />

          <div class="chat-feed__sizer" :style="{ height: `${totalSize}px` }">
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
                  :can-like="canLike(row.message)"
                  :is-moderator="permissions.revealsRemoved.value"
                  :is-editing="editingId === row.message.id"
                  :edit-text="editText"
                  :is-deleted-expanded="expandedRemoved.has(row.message.id)"
                  :max-height="MESSAGE_FOLD_HEIGHT"
                  @like="toggleLike(row.message)"
                  @toggle-deleted="toggleRemoved(row.message.id)"
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

          <FeedEdge
            v-if="hasMoreAfter"
            ref="bottomEdge"
            :error="errorAfter"
            @retry="loadNewer()"
          />
        </template>
      </div>

      <!-- The toolbar stands inside the frame, which clips it. -->
      <MessageToolbar
        v-if="
          hoveredMessage &&
          !hoveredMessage.isRemoved &&
          editingId !== hoveredMessage.id
        "
        ref="toolbar"
        :style="{
          top: toolbarPosition.top + 'px',
          right: toolbarPosition.right + 'px',
        }"
        :confirming-delete="confirmingDeleteId === hoveredMessage.id"
        :likes="hoveredMessage.likes ?? []"
        :liked="isLikedByMe(hoveredMessage)"
        :can-like="canLike(hoveredMessage)"
        :can-edit="canEdit(hoveredMessage)"
        :can-delete="canDelete(hoveredMessage)"
        :can-warn="moderation?.canWarn(hoveredMessage) ?? false"
        :link-to="linkTo(hoveredMessage.id)"
        @mouseenter="handleToolbarMouseEnter"
        @mouseleave="handleToolbarMouseLeave"
        @focusin="handleToolbarMouseEnter"
        @focusout="handleToolbarFocusOut"
        @like="toggleLike(hoveredMessage)"
        @edit="startEdit(hoveredMessage)"
        @delete="requestDelete(hoveredMessage.id)"
        @confirm-delete="confirmDelete"
        @cancel-delete="cancelDelete"
        @warn="emit('warn', hoveredMessage)"
      />

      <!-- While newer pages wait, or once the reader went a whole viewport up -->
      <JumpToLatest
        v-if="messages.length && (hasMoreAfter || isFarFromBottom)"
        @click="emit('jump-to-latest')"
      />
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
@use "@/assets/styles/ZIndex" as *

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

  // The full layout's side padding, so the compact text lands on the same x.
  &.layout-compact
    .chat-feed__message
      padding: $tiny $medium
      margin-bottom: $small
      &.removed-collapsed
        margin-bottom: $tiny
      &.continuation
        margin-top: 0

// No containment or transform here or on a row: either traps the fixed menu of an edit box.
.chat-feed__list
  // What the page spends around the feed, the search row's control included.
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

.chat-feed__sizer
  width: 100%
  position: relative

.chat-feed__row
  position: absolute
  top: 0
  left: 0
  width: 100%

// The first date keeps off the strip's dashed line; part of the row, so it is measured.
.chat-feed__row--start
  padding-top: $small

.chat-feed__message
  padding: $small $medium
  margin-bottom: $medium
  overflow-wrap: anywhere
  position: relative
  overflow: visible
  background-color: $bg-page

  &.continuation
    margin-bottom: $tiny
    margin-top: -$small

  // Square, like the highlight of every block of content.
  &:hover,
  &.hovered,
  &:focus-visible
    background-color: $bg-element

  // The row takes the focus so the keyboard reaches the toolbar; the ring goes inward.
  &:focus:not(:focus-visible)
    outline: none
  &:focus-visible
    +focus-ring($inward: true)

  &.removed
    color: $text-muted

// The overlay is laid over the editor's own fill, not in place of it.
.chat-feed__message:hover :deep(.bbcode-editor),
.chat-feed__message.hovered :deep(.bbcode-editor)
  background: linear-gradient($hover-overlay, $hover-overlay), $input-bg

// Under the frame, a step away from it.
.chat-feed__composer
  margin-top: $medium
</style>
