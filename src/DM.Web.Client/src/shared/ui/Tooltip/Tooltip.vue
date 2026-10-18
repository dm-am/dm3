<template>
  <span
    ref="triggerRef"
    class="tooltip-trigger"
    @mouseenter="handleMouseEnter"
    @mouseleave="handleMouseLeave"
    @focusin="handleFocusIn"
    @focusout="handleFocusOut"
    @pointerup="handlePointerUp"
    @click="handleClick"
    @keydown.esc="close"
    :aria-describedby="shown ? tooltipId : undefined"
    :tabindex="focusable ? 0 : undefined"
  >
    <slot />
  </span>
  <Teleport to="body">
    <Transition name="tooltip-fade">
      <TooltipBubble
        v-if="shown"
        ref="bubbleRef"
        :id="tooltipId"
        :text="text"
        :rich="hasContentSlot"
        :anchor="triggerRect"
        :placement="placement"
        @mouseenter="handleTooltipMouseEnter"
        @mouseleave="handleTooltipMouseLeave"
        @focusout="handleFocusOut"
      >
        <slot name="content">{{ text }}</slot>
      </TooltipBubble>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted, useSlots } from "vue";
import TooltipBubble from "./TooltipBubble.vue";
import {
  TAP_CLOSE_DELAY,
  TAP_MAX_DURATION,
  bubbleHasControls,
  closesByItself,
  disabledControlOf,
  isCompatMouseEvent,
  isKeyboardFocus,
  isShortPress,
  isTap,
  isTouchEvent,
  tapTargetOf,
  trackInput,
} from "./touchRule";
import type { TooltipPlacement } from "./types";

const slots = useSlots();
const hasContentSlot = computed(() => !!slots.content);

// Shows text/content on hover, on focus from the keyboard, and on a tap by the
// touch rule of touchRule.ts (WCAG 1.4.13). Trigger is only reachable by Tab
// when `focusable` is set (the caller's element already provides its own
// focus target otherwise, e.g. a <button>). Tab can continue into interactive
// tooltip content (links, buttons) without closing it — see handleFocusOut.
//
// The tooltip describes its trigger and never names it: `aria-describedby`
// lands on the wrapper span, and only while the tooltip is on screen. An
// icon-only button wrapped here carries its own `aria-label` — a description
// is announced after a name, not instead of one.
const props = withDefaults(
  defineProps<{
    /** Tooltip text. If undefined/empty, tooltip won't show */
    text?: string;
    placement?: TooltipPlacement;
    /** Delay in ms before showing/hiding */
    delay?: number;
    disabled?: boolean;
    /** Make the trigger span keyboard-focusable (tabindex="0"). Use when the
     * slotted content isn't itself a focusable element (e.g. plain text). */
    focusable?: boolean;
    /** A tap on the button inside shows the tooltip after the button acts.
     * Only for a tooltip that carries data (not the button's name) on a
     * button that leaves the reader on the page: a tag in the picker, a like.
     * Every other button and every link opens nothing on a tap. */
    showOnTap?: boolean;
  }>(),
  {
    text: undefined,
    placement: "top",
    delay: 150,
    disabled: false,
    focusable: false,
    showOnTap: false,
  },
);

// Generate unique ID for ARIA
const tooltipId = `tooltip-${Math.random().toString(36).slice(2, 9)}`;

const triggerRef = ref<HTMLElement | null>(null);
const bubbleRef = ref<InstanceType<typeof TooltipBubble> | null>(null);

/**
 * What opened the bubble, which is also what closes it: the pointer that
 * hovers leaves, the keyboard focus moves on, the tap is undone by a tap. One
 * source per bubble, so that the focus a tap gives a trigger cannot hold open
 * a bubble the tap's own rules have closed, and the focus leaving the trigger
 * for a term inside the bubble cannot close a bubble the finger opened.
 */
type OpenedBy = "hover" | "focus" | "tap";
const openedBy = ref<OpenedBy | null>(null);
const isVisible = computed(() => openedBy.value !== null);
const shown = computed(
  () =>
    isVisible.value &&
    !props.disabled &&
    !!(props.text || hasContentSlot.value),
);

const triggerRect = (): DOMRect | null =>
  triggerRef.value?.getBoundingClientRect() ?? null;

const bubbleElement = (): HTMLElement | null =>
  bubbleRef.value?.element ?? null;

let showTimer: ReturnType<typeof setTimeout> | null = null;
let hideTimer: ReturnType<typeof setTimeout> | null = null;
let tapTimer: ReturnType<typeof setTimeout> | null = null;

function clearAllTimers() {
  for (const timer of [showTimer, hideTimer, tapTimer]) {
    if (timer) clearTimeout(timer);
  }
  showTimer = null;
  hideTimer = null;
  tapTimer = null;
}

function open(by: OpenedBy) {
  clearAllTimers();
  openedBy.value = by;
  if (by === "tap") tapTimer = setTimeout(closeIfPlain, TAP_CLOSE_DELAY);
}

function close() {
  clearAllTimers();
  openedBy.value = null;
}

// A one-line bubble of plain words goes by itself. A longer one, or one with
// something to press in it, waits for the finger.
function closeIfPlain() {
  tapTimer = null;
  const bubble = bubbleElement();
  if (!bubble || closesByItself(bubble, bubbleRef.value?.content ?? null)) {
    close();
  }
}

function scheduleHide() {
  if (showTimer) clearTimeout(showTimer);
  if (hideTimer) clearTimeout(hideTimer);
  showTimer = null;
  hideTimer = setTimeout(close, props.delay);
}

// ---------------------------------------------------------------------------
// Mouse. The mouse events a browser makes up after a tap are not hover.
// ---------------------------------------------------------------------------

function handleMouseEnter() {
  if (props.disabled || isCompatMouseEvent()) return;
  if (hideTimer) clearTimeout(hideTimer);
  hideTimer = null;
  if (isVisible.value) return;
  if (showTimer) clearTimeout(showTimer);
  showTimer = setTimeout(() => open("hover"), props.delay);
}

function handleMouseLeave() {
  if (isCompatMouseEvent()) return;
  scheduleHide();
}

function handleTooltipMouseEnter() {
  // Cancel pending hide when entering tooltip
  if (isCompatMouseEvent()) return;
  if (hideTimer) clearTimeout(hideTimer);
  hideTimer = null;
}

function handleTooltipMouseLeave() {
  // Schedule hide when leaving tooltip
  if (isCompatMouseEvent()) return;
  scheduleHide();
}

// ---------------------------------------------------------------------------
// Keyboard: show immediately on focus (no delay), hide when focus leaves
// ---------------------------------------------------------------------------

function handleFocusIn(e: FocusEvent) {
  if (props.disabled || !isKeyboardFocus(e.target)) return;
  open("focus");
}

function handleFocusOut(e: FocusEvent) {
  // Ignore focus moves within the trigger (e.g. between its children)
  const next = e.relatedTarget as Node | null;
  if (next && triggerRef.value?.contains(next)) return;
  // Keep tooltip open when focus moves into the tooltip itself, symmetric
  // to handleTooltipMouseEnter — lets Tab reach links inside tooltip content
  // (e.g. award popovers) instead of closing on the way there
  if (next && bubbleElement()?.contains(next)) return;
  // Only the keyboard's bubble goes with the focus. A tap on a term inside a
  // bubble the finger opened takes the focus off the trigger (the term takes
  // none), and that closed the bubble under the finger.
  if (openedBy.value !== "focus") return;
  close();
}

// ---------------------------------------------------------------------------
// Touch, by the rule of touchRule.ts
// ---------------------------------------------------------------------------

// When the tap of a disabled control was taken from pointerup, so that a click
// some browser may still send for it does not undo it.
let tapTakenAt = Number.NEGATIVE_INFINITY;

function toggleByTap() {
  if (openedBy.value === "tap") close();
  else open("tap");
}

// A disabled control sends no click, and its tooltip is the one that says why
// it is disabled. Its tap is read from the pointer: released where it went
// down, and soon (the press was measured on the document a moment before).
function handlePointerUp(e: PointerEvent) {
  if (props.disabled || !triggerRef.value || !isTouchEvent(e)) return;
  if (!disabledControlOf(e.target, triggerRef.value) || !isShortPress()) {
    return;
  }
  tapTakenAt = e.timeStamp;
  toggleByTap();
}

function handleClick(e: MouseEvent) {
  if (props.disabled || !isTap(e)) return;
  if (e.timeStamp - tapTakenAt < TAP_MAX_DURATION) return;
  switch (tapTargetOf(e.target)) {
    case "link":
      return;
    case "control":
      // The button has acted; its data follows, and a second tap on it acts
      // again rather than hiding what it just showed.
      if (props.showOnTap) open("tap");
      return;
    case "text":
      toggleByTap();
  }
}

// While a bubble is up: a press anywhere else closes it, and so does a scroll
// under a bubble the finger opened, which would otherwise ride along with its
// trigger or stick to the edge of the window once the trigger has left it.
function handleDocumentPointerDown(e: Event) {
  const target = e.target as Node | null;
  if (!target) return;
  // The trigger's own tap decides for itself (handleClick).
  if (triggerRef.value?.contains(target)) return;
  const bubble = bubbleElement();
  if (bubble?.contains(target)) {
    // A bubble of plain words has nothing to reach: a finger on it wants it
    // out of the way. One with a link or a term stays for them.
    if (openedBy.value === "tap" && !bubbleHasControls(bubble)) close();
    return;
  }
  close();
}

function handleScroll() {
  if (openedBy.value === "tap") close();
}

function listen(on: boolean) {
  const method = on ? "addEventListener" : "removeEventListener";
  document[method]("pointerdown", handleDocumentPointerDown, true);
  window[method]("scroll", handleScroll, true);
}

watch(isVisible, (visible) => listen(visible));

onMounted(trackInput);

// Cleanup
onUnmounted(() => {
  clearAllTimers();
  listen(false);
});
</script>

<style scoped lang="sass">
// The bubble's own look lives in TooltipBubble.vue.
.tooltip-trigger
  // No styles - behaves as inline text
  display: inline
</style>
