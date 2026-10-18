<script setup lang="ts">
/**
 * UnreadLine — "Непрочитанных: 4 | Отметить прочитанными" under the heading of
 * a page that marks everything it counts as read at once: the notifications
 * and the forum. One action, so one look in both places; it used to be a large
 * grey button beside one heading and a shaded one over a table on the other.
 *
 * The line is a run of muted secondary text, the number bold green as the
 * counters of the header are, the action a plain link of the site whose
 * target a finger finds 32 high without the line growing. It stands only while
 * something is unread: an action with nothing to act on is not offered. From a
 * thousand the number reads "999+".
 *
 * It is a caption line of the site (CaptionLine) in the size of secondary
 * text: the number with its word is one part and the link another, so where
 * the column is narrower than the line ("999+" on a phone of 360) the link
 * takes the next line whole, with no bar at either end. The word is
 * "Непрочитанных:" at every width (owner, 12 October 2026: never shorten a
 * thing on a phone; it used to turn into "Новых:" where the line had no room).
 *
 * After a press a refusal is said in red under the line, and the line stays
 * for another try. Success is the page's to show (it marks its rows and its
 * count falls to zero, which takes the line away); the line says it to a
 * screen reader and puts the focus on the heading it stood under, since the
 * link that held the focus is gone and focus on a removed node falls to the
 * document.
 */
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

// Something new arrived after a press: what was said about it is stale, and so
// is a refusal about a count that is not the one on the line any more.
watch(
  () => props.count,
  (count, previous) => {
    if (count > previous) announced.value = "";
    if (count === 0) result.clear();
  },
);

/**
 * The page heading the line stands under: the last h1 before it in its page.
 * It takes the focus from script only, so it is given tabindex -1 if the page
 * did not give it one, which keeps it out of the Tab order.
 */
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
  <!-- The space between the word and the number is text inside the part: the
       template compiler drops whitespace that holds a line break between two
       tags, and they would run together on the screen and in a copy. The bar
       between the parts is the caption line's. -->
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
@use "@/assets/styles/Inputs" as *

// The height a finger finds the link by. An overlay out of the flow, so the
// line itself does not grow, and a press on it is a press on the link.
$mark-target-height: 32px

// The size of the line's text, which the caption line repeats, set on the box
// so the paragraph around the line stands on the line's height.
.unread-place
  font-size: $secondary-font-size

// One step down to what it counts: the list, the table.
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

  // Waiting for the server: the look of a link out of reach, while the focus
  // stays where the reader left it (a disabled button drops it).
  &[aria-busy="true"]
    opacity: $disabled-opacity
    cursor: default

// Under the line, one step over the list the line keeps.
.unread-refusal
  --action-result-margin: calc(#{$small} - #{$medium}) 0 #{$medium}
</style>
