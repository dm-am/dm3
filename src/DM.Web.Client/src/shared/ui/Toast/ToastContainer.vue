<template>
  <Teleport to="body">
    <!-- No aria-live on the container: it mounts together with the first
         toast (v-if), so a container live region would not pre-exist in the
         DOM and the first announcement would be lost. Per-toast role="alert"
         / role="status" announces on insertion instead — exactly one live
         region per toast, no double announcements. -->
    <div class="toast-container" v-if="toasts.length">
      <TransitionGroup name="toast">
        <div
          v-for="{ toast, link } in items"
          :key="toast.id"
          class="toast-item"
          :class="`toast-${toast.type}`"
          :role="toast.type === 'error' ? 'alert' : 'status'"
          @mouseenter="pause(toast.id)"
          @mouseleave="resume(toast.id)"
        >
          <span v-if="link" class="toast-message"
            >{{ link.before
            }}<button class="toast-action" @click="act(toast.id, toast.action)">
              {{ link.label }}</button
            >{{ link.after }}</span
          >
          <span v-else class="toast-message">{{ toast.message }}</span>
          <!-- Empty on purpose: the two bars of the cross are drawn by the
               stylesheet, as in the site's windows. -->
          <button
            type="button"
            class="toast-dismiss"
            @click="dismiss(toast.id)"
            aria-label="Закрыть"
          ></button>
          <div
            v-if="toast.duration > 0"
            class="toast-progress"
            :style="{ width: `${(toast.remaining / toast.duration) * 100}%` }"
          />
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useToast, type ToastAction } from "@/shared/lib/composables/useToast";

const { toasts, dismiss, pause, resume } = useToast();

/**
 * The words a message marks as its press: the first pair of square brackets in
 * the text, and only where the toast carries something to run.
 *
 * The mark is in the message rather than in a label beside it, because the
 * press belongs to a place in the sentence. Handed in separately it can only
 * be appended, and an appended word either repeats what the sentence already
 * says or asks the reader to read the line twice to find what it refers to.
 */
const MARKED = /^([^[]*)\[([^\]]+)\]([\s\S]*)$/;

const items = computed(() =>
  toasts.value.map((toast) => {
    const found = toast.action ? MARKED.exec(toast.message) : null;
    return {
      toast,
      link: found
        ? { before: found[1], label: found[2], after: found[3] }
        : null,
    };
  }),
);

function act(id: string, run?: ToastAction) {
  run?.();
  dismiss(id);
}
</script>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/Inputs" as *

// Pinned to the bottom-right corner, and so is the ScrollNav rail — which
// sits on $z-sticky, two tiers below the toast. A toast used to land on top
// of the scroll buttons and the settings one, hiding the controls a reader
// reaches for while the message is still on screen. Stepping aside by the
// width of the rail plus a gap keeps both usable. Wider than the shell
// breakpoint the rail stands $medium from the edge; below it the rail stands
// in the gutter of the page, closer to the edge, so the step aside holds there
// too.
// The one thing a message offers to do about itself, and it stands in the
// place in the sentence that names it: the address to try, the verb to run.
// Underlined and in the ink of the message, the way a link inside a paragraph
// is drawn everywhere else on the site - bold would make a word of the
// sentence shout, and a control at the edge of the box would read as the act
// of the screen, which a message that dismisses itself never carries.
.toast-action
  +inline-link-button
  color: inherit
  text-decoration: underline

.toast-container
  position: fixed
  bottom: $medium
  right: $medium + $scroll-nav-width + $small
  z-index: $z-toast
  display: flex
  flex-direction: column
  gap: $small
  // The stack hugs the right edge and every message is as wide as its own
  // words. One width for all kept the left edge of the stack straight at the
  // price of half an empty box under "Секрет скопирован", and a short message
  // inside a wide frame reads as one that was cut off.
  //
  // The cap is set above the longest line any message can be, so on a desktop
  // nothing here ever wraps and the box always hugs its own sentence. A cap
  // below that length is what put half a line of nothing under a message: the
  // box goes to the cap, the words go to two lines, and the cap is what the
  // reader sees. toastWidth.spec.ts holds messages to the length this cap
  // fits - 64 characters, 421 pixels at the longest message the site raises -
  // which is what keeps the empty half from coming back.
  align-items: flex-end
  max-width: 620px

// A panel like every other panel on the site: the surface of the theme, the
// ink of the theme, and a frame in the accent of the kind of message. It used
// to be twelve hand-picked Material colours - a saturated fill with white
// letters that appeared nowhere else on the page and belonged to neither
// palette. The frame is what keeps
// the panel off whatever it floats over; the shadow alone is not an edge.
.toast-item
  position: relative
  display: flex
  align-items: center
  justify-content: space-between
  padding: $small $medium
  border: 1px solid $border
  border-radius: $border-radius
  font-size: $secondary-font-size
  color: $text
  box-shadow: 0 2px 8px $shadow-color
  // As wide as the words, and never wider than the column.
  max-width: 100%
  overflow: hidden

// The kind of the message colours the whole frame the rule above has already
// drawn on all four sides, in the border tone of that colour. A rule down one
// edge in an accent is the mark of a quote and of a [mod] block inside post
// text, and it belongs to those two alone: anywhere else it reads as a second
// kind of card.
.toast-success
  background-color: $bg-highlight-green
  border-color: $border-accent-green

.toast-error
  background-color: $bg-highlight-red
  border-color: $border-accent-red

.toast-info
  background-color: $bg-highlight-blue
  border-color: $border-accent-blue

.toast-warning
  background-color: $bg-highlight-yellow
  border-color: $border-accent-yellow

.toast-message
  flex: 0 1 auto
  margin-right: $small
  // No balancing. Balance evens the two lines of a wrapped message, and in a
  // box that does not shrink to them that is exactly what leaves a gap down
  // the right of both. A narrow screen wraps greedily instead: the first line
  // is full, the second is what is left, and the only space is after the last
  // word, which is what every wrapped paragraph looks like.

// The cross of the plate, drawn out of two bars, as the windows draw theirs:
// the glyph "×" it replaces was as thick as the font that happened to load
// made it. The owner asked for the bars in the very look the glyph had, so
// every number below is the glyph's, fitted on the stand at four times the
// size against a frame of the glyph (27.09.2026, PT Sans): the box it gave the
// button (a line of 1.2rem by its advance and $tiny either side), the length
// and the stroke of its arms, and where their crossing stood, a little under
// the middle of the line, on the maths axis of the font. The ink and its
// opacity are the glyph's too.
$toast-cross-box-width: 13.703125px
$toast-cross-box-height: 25px
// Length and stroke of an arm, in pixels (unitless: they are scale factors).
$toast-cross-bar: 8.25
$toast-cross-stroke: 1.4
// The crossing, from the right edge and from the top of the box.
$toast-cross-x: 6.8px
$toast-cross-y: 13.5px

.toast-dismiss
  position: relative
  flex: none
  width: $toast-cross-box-width
  height: $toast-cross-box-height
  padding: 0
  background: none
  border: none
  color: inherit
  cursor: pointer
  opacity: 0.8
  &:hover
    opacity: 1

  // Each arm is a box of one pixel in the top right corner of the button,
  // stretched to its length and stroke, turned, and carried to the crossing,
  // all by the transform. A box with a transform is laid out on whole pixels:
  // an arm 1.4 pixels thick given as a height is painted one pixel thick, and
  // one moved by its offsets rounds to a whole pixel as well. The corner of a
  // plate of one line stands on one (the plate hugs a fixed corner of the
  // screen), and a transform keeps its fractions, so the arms land where the
  // glyph stood.
  &::before,
  &::after
    content: ""
    position: absolute
    top: 0
    right: 0
    width: 1px
    height: 1px
    background-color: currentColor

  $to-crossing: translate(0.5px - $toast-cross-x, $toast-cross-y - 0.5px)

  &::before
    transform: $to-crossing rotate(45deg) scale($toast-cross-bar, $toast-cross-stroke)

  &::after
    transform: $to-crossing rotate(-45deg) scale($toast-cross-bar, $toast-cross-stroke)

.toast-progress
  position: absolute
  bottom: 0
  left: 0
  height: 3px
  background: $overlay-subtle
  transition: width 0.1s linear
  @media (prefers-reduced-motion: reduce)
    transition: none

.toast-enter-active,
.toast-leave-active
  transition: opacity 0.3s ease, transform 0.3s ease
  @media (prefers-reduced-motion: reduce)
    transition: none

.toast-enter-from
  opacity: 0
  transform: translateX(100%)

.toast-leave-to
  opacity: 0
  transform: translateX(100%)
</style>
