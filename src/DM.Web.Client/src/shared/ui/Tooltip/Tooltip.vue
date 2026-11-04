<script setup lang="ts">
import {
  ref,
  computed,
  watch,
  onMounted,
  onUnmounted,
  useId,
  useSlots,
} from "vue";
import TooltipBubble from "./TooltipBubble.vue";
import { isKeyboardFocus, trackInput } from "./inputSource";
import type { TooltipPlacement } from "./types";

const slots = useSlots();
const hasContentSlot = computed(() => !!slots.content);

// The tooltip describes its trigger and never names it: an icon-only button wrapped here keeps its own aria-label.
/** Before a hover shows the tooltip and before leaving hides it, in ms. */
const HOVER_DELAY = 150;

const props = withDefaults(
  defineProps<{
    /** Tooltip text. If undefined/empty, tooltip won't show */
    text?: string;
    placement?: TooltipPlacement;
    disabled?: boolean;
    /** tabindex="0" on the trigger, for slotted content that is not focusable itself. */
    focusable?: boolean;
  }>(),
  {
    text: undefined,
    placement: "top",
    disabled: false,
    focusable: false,
  },
);

const tooltipId = `tooltip-${useId()}`;

const triggerRef = ref<HTMLElement | null>(null);
const bubbleRef = ref<InstanceType<typeof TooltipBubble> | null>(null);

/** What opened the bubble closes it: one source per bubble, so focus and hover cannot undo each other. */
type OpenedBy = "hover" | "focus";
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

function clearAllTimers() {
  for (const timer of [showTimer, hideTimer]) {
    if (timer) clearTimeout(timer);
  }
  showTimer = null;
  hideTimer = null;
}

function open(by: OpenedBy) {
  clearAllTimers();
  openedBy.value = by;
}

function close() {
  clearAllTimers();
  openedBy.value = null;
}

function scheduleHide() {
  if (showTimer) clearTimeout(showTimer);
  if (hideTimer) clearTimeout(hideTimer);
  showTimer = null;
  hideTimer = setTimeout(close, HOVER_DELAY);
}

function handleMouseEnter() {
  if (props.disabled) return;
  if (hideTimer) clearTimeout(hideTimer);
  hideTimer = null;
  if (isVisible.value) return;
  if (showTimer) clearTimeout(showTimer);
  showTimer = setTimeout(() => open("hover"), HOVER_DELAY);
}

function handleMouseLeave() {
  scheduleHide();
}

function handleTooltipMouseEnter() {
  if (hideTimer) clearTimeout(hideTimer);
  hideTimer = null;
}

function handleTooltipMouseLeave() {
  scheduleHide();
}

function handleFocusIn(e: FocusEvent) {
  if (props.disabled || !isKeyboardFocus(e.target)) return;
  open("focus");
}

function handleFocusOut(e: FocusEvent) {
  const next = e.relatedTarget as Node | null;
  if (next && triggerRef.value?.contains(next)) return;
  // Into the tooltip itself: Tab reaches the links inside.
  if (next && bubbleElement()?.contains(next)) return;
  // Only the keyboard's bubble goes with the focus.
  if (openedBy.value !== "focus") return;
  close();
}

// A press outside the trigger and the bubble closes the bubble.
function handleDocumentPointerDown(e: Event) {
  const target = e.target as Node | null;
  if (!target) return;
  if (triggerRef.value?.contains(target)) return;
  if (bubbleElement()?.contains(target)) return;
  close();
}

function listen(on: boolean) {
  const method = on ? "addEventListener" : "removeEventListener";
  document[method]("pointerdown", handleDocumentPointerDown, true);
}

watch(isVisible, (visible) => listen(visible));

onMounted(trackInput);

onUnmounted(() => {
  clearAllTimers();
  listen(false);
});
</script>

<template>
  <span
    ref="triggerRef"
    class="tooltip-trigger"
    @mouseenter="handleMouseEnter"
    @mouseleave="handleMouseLeave"
    @focusin="handleFocusIn"
    @focusout="handleFocusOut"
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

<style scoped lang="sass">
// The bubble's own look lives in TooltipBubble.vue.
.tooltip-trigger
  // No styles - behaves as inline text
  display: inline
</style>
