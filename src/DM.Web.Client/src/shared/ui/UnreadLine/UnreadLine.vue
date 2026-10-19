<script setup lang="ts">
// Stands only while something is unread. On success the focus goes to the heading above: the link that
// held it is gone, and focus on a removed node falls to the document.
import { computed, nextTick, ref, watch } from "vue";
import type { GeneralError } from "@/shared/api/models/common";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { CaptionLine } from "@/shared/ui/CaptionLine";

const props = defineProps<{
  /** How many are unread. Nothing is drawn while there are none. */
  count: number;
  /** Marks everything read. Resolves with the refusal, or null once done. */
  mark: () => Promise<GeneralError | null>;
  /** The refusal's own sentence, for a server that named nothing. */
  failure: string;
  /** What a screen reader hears once everything is marked. */
  done: string;
}>();

/** The largest number written as itself; past it the line says "999+". */
const SHOWN_MAX = 999;

const shown = computed(() =>
  props.count > SHOWN_MAX ? `${SHOWN_MAX}+` : String(props.count),
);

const root = ref<HTMLElement | null>(null);
const result = useActionResult();
const busy = ref(false);
const announced = ref("");

// A new arrival makes what was said about the last count stale.
watch(
  () => props.count,
  (count, previous) => {
    if (count > previous) announced.value = "";
    if (count === 0) result.clear();
  },
);

/** The last h1 before the line; tabindex -1 lets script focus it without entering the Tab order. */
function headingAbove(el: HTMLElement): HTMLElement | null {
  const scope = el.closest("main") ?? el.ownerDocument;
  let heading: HTMLElement | null = null;
  for (const h of scope.querySelectorAll<HTMLElement>("h1")) {
    if (h.compareDocumentPosition(el) & Node.DOCUMENT_POSITION_FOLLOWING) {
      heading = h;
    }
  }
  return heading;
}

async function press() {
  if (busy.value) return;
  busy.value = true;
  result.clear();
  announced.value = "";
  let error: GeneralError | null;
  try {
    error = await props.mark();
  } finally {
    busy.value = false;
  }
  if (error) {
    result.refuse(error, props.failure);
    return;
  }
  announced.value = props.done;
  await nextTick();
  const heading = root.value && headingAbove(root.value);
  if (!heading) return;
  if (!heading.hasAttribute("tabindex")) heading.setAttribute("tabindex", "-1");
  heading.focus();
}
</script>

<template>
  <!-- The space after the word is text inside the part: the compiler drops whitespace holding a line break. -->
  <div ref="root" class="unread-place">
    <p v-if="count > 0" class="unread-line">
      <CaptionLine size="secondary">
        <span class="unread-label"
          >Непрочитанных:{{ " "
          }}<span class="unread-count">{{ shown }}</span></span
        >
        <button
          type="button"
          class="unread-mark"
          :aria-busy="busy || undefined"
          @click="press"
        >
          Отметить прочитанными
        </button>
      </CaptionLine>
    </p>
    <ActionResult :result="result" class="unread-refusal" />
    <p class="visually-hidden" role="status">{{ announced }}</p>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

// A finger's target, out of the flow, so the line itself does not grow.
$mark-target-height: 32px

// The line's text size on the box, so the paragraph stands on the line's height.
.unread-place
  font-size: $secondary-font-size

.unread-line
  margin: 0 0 $medium

.unread-count
  font-weight: bold
  color: $accent-green

.unread-mark
  position: relative
  +inline-link-button
  &::after
    content: ""
    position: absolute
    top: 50%
    left: 0
    right: 0
    height: $mark-target-height
    transform: translateY(-50%)

  // A link out of reach while the focus stays: a disabled button drops it.
  &[aria-busy="true"]
    opacity: $disabled-opacity
    cursor: default

.unread-refusal
  --action-result-margin: calc(#{$small} - #{$medium}) 0 #{$medium}
</style>
