<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import TooltipBubble from "./TooltipBubble.vue";
import {
  isCompatMouseEvent,
  isKeyboardFocus,
  isTap,
  trackInput,
} from "./touchRule";

/**
 * The words of a tooltip: a tag description as the server renders it, or a
 * plain text the client writes itself ("в группе уже выбран тег").
 *
 * A description is the hint surface (BbSurface.Hint), whose one tag is [tip].
 * The server draws each such word as a span.bb-tip carrying what it shows in
 * data-tip-text and data-tip-img, and nothing of the picture itself: this
 * component wires the words, and draws the small tooltip over the word that
 * is open, from the attributes, by binding and not by markup. The picture is
 * requested when a reader opens it, not with the list of tags on every page.
 * The only markup put into the page is the server's (BBCODE_RENDERING.md),
 * or the preview's in the window of the tag, which draws the same string.
 *
 * A word opens on hover, on a tap and on a focus from the keyboard. The tap is
 * the one of touchRule.ts, the rule Tooltip.vue gives its own trigger: a
 * reader meets the two as one control. Inside a tooltip carried to <body>
 * Tab does not reach the words, so the keyboard reads them where the text
 * stands in the page: the card of the tag and the preview in its window.
 *
 * The small tooltip is the site's bubble, framed to stand apart from the one
 * under it (TooltipBubble, nested). Its words wrap at 280, the width of a
 * picture, and it stays inside the window: over the word, or under it when
 * there is no room above.
 */
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
const openedBy = ref<"hover" | "tap" | "focus" | null>(null);

let hideTimer: ReturnType<typeof setTimeout> | null = null;

function cancelHide() {
  if (hideTimer) clearTimeout(hideTimer);
  hideTimer = null;
}

function open(word: HTMLElement, by: "hover" | "tap" | "focus") {
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

/** The word of a [tip] the event reached, inside this text. */
function wordOf(target: EventTarget | null): HTMLElement | null {
  if (!(target instanceof Element)) return null;
  const word = target.closest<HTMLElement>(".bb-tip");
  return word && rootRef.value?.contains(word) ? word : null;
}

/** Whether a node is inside the small tooltip. */
function inBubble(node: EventTarget | null): boolean {
  const bubble = bubbleRef.value?.element;
  return !!bubble && node instanceof Node && bubble.contains(node);
}

// ---------------------------------------------------------------------------
// The words are the server's markup: one listener each on the text for the
// pointer, the finger and the focus, read by the word they reached.
// ---------------------------------------------------------------------------

// Hover, by the mouse only: the mouse events a browser makes up after a tap
// would otherwise open the popup a second time behind the tap's back.
function handleMouseOver(event: MouseEvent) {
  if (isCompatMouseEvent() || inBubble(event.target)) return;
  const word = wordOf(event.target);
  if (word) open(word, "hover");
}

function handleMouseOut(event: MouseEvent) {
  if (isCompatMouseEvent() || !wordOf(event.target)) return;
  // Moving within the word, or onto its small tooltip, is not leaving it.
  const next = event.relatedTarget;
  if (wordOf(next) === active.value || inBubble(next)) return;
  scheduleClose();
}

// A tap, read from the click the browser sends for it (touchRule.ts): a scroll
// that starts on a word and a long press that selects it open nothing. The
// small tooltip opens at once, and a second tap on the word closes it; a tap
// on neither the word nor its tooltip closes it (handleDocumentPointerDown).
function handleClick(event: MouseEvent) {
  if (!isTap(event) || inBubble(event.target)) return;
  const word = wordOf(event.target);
  if (!word) return;
  if (active.value === word && openedBy.value === "tap") close();
  else open(word, "tap");
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

// A tooltip renders this component behind a v-if, so closing it unmounts the
// text and takes an open small tooltip with it. What does not go by itself is
// this listener and a pending close, hence both by hand.
onUnmounted(() => {
  cancelHide();
  document.removeEventListener("pointerdown", handleDocumentPointerDown, {
    capture: true,
  });
});

// ---------------------------------------------------------------------------
// The small tooltip, from the attributes of the open word
// ---------------------------------------------------------------------------

const tipText = computed(() => active.value?.dataset.tipText ?? null);
const tipImage = computed(() => active.value?.dataset.tipImg ?? null);

// Pictures that failed to load, by address. The failure is state Vue renders
// from, not a node the handler swaps by hand: replacing the <img> from the
// event took the element out from under the patcher.
const failedImages = ref(new Set<string>());

const imageFailed = computed(
  () => tipImage.value !== null && failedImages.value.has(tipImage.value),
);

// Takes the address the failing element was drawn with: an error can arrive
// after the pointer has moved on, and the open word is no longer the one that
// failed by then.
function markImageFailed(address: string) {
  failedImages.value = new Set(failedImages.value).add(address);
}

// A picture that loads changes the size of the tooltip, which is placed by
// its size: placed again once the picture is in.
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
    @click="handleClick"
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
      :rich="!!tipImage"
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
