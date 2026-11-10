<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import TooltipBubble from "./TooltipBubble.vue";
import { isKeyboardFocus, trackInput } from "./inputSource";
import type { OpenedBy } from "./types";

// The server draws each [tip] word as a span.bb-tip with data-tip-text and data-tip-img; this component wires
// the words and binds the small tooltip from the attributes, so no markup but the server's enters the page.
withDefaults(
  defineProps<{
    /** The server's markup of a tag description. */
    html?: string | null;
    /** Plain text, for a tooltip the client writes itself. */
    text?: string | null;
  }>(),
  {
    html: null,
    text: null,
  },
);

/** The widest the small tooltip grows: the width of its picture. */
const TIP_WIDTH = 280 + 2 * 16 + 2;

/** How long a word waits for the pointer to come back before it closes. */
const HIDE_DELAY = 200;

const rootRef = ref<HTMLElement | null>(null);
const bubbleRef = ref<InstanceType<typeof TooltipBubble> | null>(null);

/** The word whose small tooltip is open, and how it was opened. */
const active = ref<HTMLElement | null>(null);
const openedBy = ref<OpenedBy | null>(null);

let hideTimer: ReturnType<typeof setTimeout> | null = null;

function cancelHide() {
  if (hideTimer) clearTimeout(hideTimer);
  hideTimer = null;
}

function open(word: HTMLElement, by: OpenedBy) {
  cancelHide();
  active.value = word;
  openedBy.value = by;
}

function close() {
  cancelHide();
  active.value = null;
  openedBy.value = null;
}

function scheduleClose() {
  cancelHide();
  hideTimer = setTimeout(close, HIDE_DELAY);
}

function wordOf(target: EventTarget | null): HTMLElement | null {
  if (!(target instanceof Element)) return null;
  const word = target.closest<HTMLElement>(".bb-tip");
  return word && rootRef.value?.contains(word) ? word : null;
}

function inBubble(node: EventTarget | null): boolean {
  const bubble = bubbleRef.value?.element;
  return !!bubble && node instanceof Node && bubble.contains(node);
}

// One listener each on the text, read by the word the event reached: the words are the server's markup.

function handleMouseOver(event: MouseEvent) {
  if (inBubble(event.target)) return;
  const word = wordOf(event.target);
  if (word) open(word, "hover");
}

function handleMouseOut(event: MouseEvent) {
  if (!wordOf(event.target)) return;
  // Moving within the word, or onto its small tooltip, is not leaving it.
  const next = event.relatedTarget;
  if (wordOf(next) === active.value || inBubble(next)) return;
  scheduleClose();
}

function handleFocusIn(event: FocusEvent) {
  const word = wordOf(event.target);
  if (word && isKeyboardFocus(word)) open(word, "focus");
}

function handleFocusOut(event: FocusEvent) {
  if (openedBy.value !== "focus") return;
  if (wordOf(event.relatedTarget) === active.value) return;
  close();
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === "Escape" && active.value) close();
}

function handleDocumentPointerDown(event: Event) {
  if (!active.value) return;
  const target = event.target;
  if (inBubble(target) || wordOf(target) === active.value) return;
  close();
}

onMounted(() => {
  trackInput();
  document.addEventListener("pointerdown", handleDocumentPointerDown, {
    capture: true,
    passive: true,
  });
});

// Closing the tooltip unmounts this text; the document listener and a pending close do not go by themselves.
onUnmounted(() => {
  cancelHide();
  document.removeEventListener("pointerdown", handleDocumentPointerDown, {
    capture: true,
  });
});

const tipText = computed(() => active.value?.dataset.tipText ?? null);
const tipImage = computed(() => active.value?.dataset.tipImg ?? null);

// State Vue renders from: swapping the <img> in the handler took it from under the patcher.
const failedImages = ref(new Set<string>());

const imageFailed = computed(
  () => tipImage.value !== null && failedImages.value.has(tipImage.value),
);

// The address the element was drawn with: the open word may have changed by the time of the error.
function markImageFailed(address: string) {
  failedImages.value = new Set(failedImages.value).add(address);
}

// A loaded picture changes the tooltip's size, which places it.
function placeAgain() {
  bubbleRef.value?.reposition();
}

const anchor = (): DOMRect | null =>
  active.value?.getBoundingClientRect() ?? null;
</script>

<template>
  <span
    ref="rootRef"
    class="rich-text"
    @mouseover="handleMouseOver"
    @mouseout="handleMouseOut"
    @focusin="handleFocusIn"
    @focusout="handleFocusOut"
    @keydown="handleKeydown"
    ><span v-if="html" class="rich-text-body" v-html="html"></span
    ><template v-else>{{ text }}</template
    ><TooltipBubble
      v-if="active && (tipText || tipImage)"
      ref="bubbleRef"
      :key="tipImage ?? tipText ?? ''"
      nested
      :text="tipImage ? undefined : (tipText ?? undefined)"
      :anchor="anchor"
      :max-width="TIP_WIDTH"
      @mouseenter="cancelHide"
      @mouseleave="scheduleClose"
      ><template v-if="tipImage"
        ><span v-if="imageFailed" class="rich-text-error"
          >Не удалось загрузить изображение</span
        ><img
          v-else
          :src="tipImage"
          alt=""
          class="rich-text-image"
          referrerpolicy="no-referrer"
          @load="placeAgain"
          @error="markImageFailed(tipImage)"
        /><span v-if="tipText" class="rich-text-caption">{{
          tipText
        }}</span></template
      ><template v-else>{{ tipText }}</template></TooltipBubble
    ></span
  >
</template>

<style lang="sass">
// Global (not scoped): the words are the server's markup, which a scoped rule
// does not reach, and the text renders inside the teleported tooltip.
.rich-text .bb-tip
  text-decoration: underline dotted
  text-decoration-color: currentColor
  text-underline-offset: 3px
  cursor: help

.rich-text-image
  display: block
  max-width: 280px
  max-height: 200px
  width: auto
  height: auto

// A picture with its text: the text under it, wrapping at its width.
.rich-text-caption
  display: block
  margin-top: $small
  white-space: normal

// The theme's red, as every refusal of the site.
.rich-text-error
  color: $accent-red
</style>
