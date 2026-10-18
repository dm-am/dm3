<script setup lang="ts">
/**
 * The sliding column of the shell on a narrow screen: slid out past the left
 * edge of the screen and brought in over the page by the button of the
 * header.
 *
 * It holds the shell's own columns, not copies of them. The old drawer
 * rendered the left column a second time inside itself, with its own requests
 * at every opening; here the shell puts its columns into this frame, so what
 * they hold, how far they are scrolled and which of their blocks are folded
 * stays as it was between two openings. What it holds scrolls in one element,
 * handed out as `content`, so a caller that shows one of several views in it
 * can keep the scroll of each view apart.
 *
 * Out over the page it is a modal, and owes what every modal owes: focus in on
 * open, kept inside while open, back where it was on close, Escape closes
 * (useDialogShell). It holds the page still under it with the lock the windows
 * use. Back in, it is inert, so Tab never walks into a column off the screen.
 *
 * It opens on a bar: what the caller puts there (the tabs of its views, or by
 * default the name of the column in the heading style of a window), the cross
 * of a window in the corner, and a line of the theme under the two. The
 * column scrolls under that line while the bar stands, which is also how a
 * reader sees there is more below.
 *
 * Closing has more ways than a window has, because this is navigation:
 * - a press on any link inside, even one to the page already open, which
 *   leaves the address as it was and would otherwise look like nothing
 *   happened;
 * - the "back" gesture of Android, through CloseWatcher where the browser has
 *   one, so the gesture shuts the column instead of leaving a page that may
 *   hold typed text. No entry is added to the history for it: elsewhere "back"
 *   leaves the page, and the shell closes the column on the way.
 */
import { computed, onBeforeUnmount, ref, watch } from "vue";
import { useDialogShell } from "@/shared/lib/composables/useDialogShell";
import { usePageScrollLock } from "@/shared/lib/composables/usePageScrollLock";

defineOptions({ inheritAttrs: false });

const props = defineProps<{
  /** Whether the column is out over the page. */
  open: boolean;
  /**
   * The name of the column, the name of the button that opens it: read out as
   * its accessible name, and written in its bar when the caller puts nothing
   * else there.
   */
  label: string;
}>();

const emit = defineEmits<{
  (e: "update:open", value: boolean): void;
}>();

const panel = ref<HTMLElement | null>(null);
const closeBtn = ref<HTMLButtonElement | null>(null);
/** The element that scrolls under the bar. */
const content = ref<HTMLElement | null>(null);

defineExpose({ content });

function close() {
  emit("update:open", false);
}

const shown = computed(() => props.open);

const { handleKeydown } = useDialogShell({
  show: shown,
  container: panel,
  initialFocus: () => closeBtn.value,
  onDismiss: close,
});

const page = usePageScrollLock();
watch(shown, (open) => (open ? page.hold() : page.release()), {
  immediate: true,
});

/** A press on a link inside the column is a choice made: the column goes. */
function closeOnLink(e: MouseEvent) {
  const target = e.target as Element | null;
  if (target?.closest?.("a[href]")) close();
}

/**
 * The part of CloseWatcher this uses. Not in the DOM library of TypeScript
 * yet, and not in every browser: where it is missing, Escape and the other
 * ways out remain.
 */
interface BackWatcher {
  onclose: (() => void) | null;
  destroy(): void;
}

let backWatcher: BackWatcher | null = null;

function watchBack() {
  const Watcher = (window as { CloseWatcher?: new () => BackWatcher })
    .CloseWatcher;
  if (!Watcher || backWatcher) return;
  try {
    backWatcher = new Watcher();
  } catch {
    return;
  }
  backWatcher.onclose = () => {
    backWatcher = null;
    close();
  };
}

function unwatchBack() {
  backWatcher?.destroy();
  backWatcher = null;
}

watch(shown, (open) => (open ? watchBack() : unwatchBack()), {
  immediate: true,
});

onBeforeUnmount(unwatchBack);
</script>

<template>
  <Transition name="drawer-scrim">
    <div v-if="open" class="drawer-scrim" aria-hidden="true" @click="close" />
  </Transition>
  <!-- tabindex="-1": the keydown listener is on the column, and a click on its
       own text would otherwise drop focus to <body>, outside it, where Escape
       never reaches the listener. -->
  <aside
    ref="panel"
    v-bind="$attrs"
    class="drawer-panel"
    :class="{ 'drawer-panel--open': open }"
    tabindex="-1"
    role="dialog"
    aria-modal="true"
    :aria-label="label"
    :inert="!open"
    @keydown="handleKeydown"
    @click="closeOnLink"
  >
    <!-- The bar sits outside the scrolling region, not over it: what it holds
         and the cross stand while the column scrolls under the line. The cross
         is in the right corner, where a window has it. -->
    <div class="drawer-header">
      <slot name="bar">
        <h2 class="drawer-title">{{ label }}</h2>
      </slot>
      <!-- Empty on purpose: the two bars of the cross are drawn by the
           stylesheet, and a glyph inside would stand behind them. -->
      <button
        ref="closeBtn"
        type="button"
        class="drawer-close"
        aria-label="Закрыть"
        @click="close"
      ></button>
    </div>
    <div ref="content" class="drawer-content">
      <slot />
    </div>
  </aside>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/DialogSurface" as *

// The dimming of the windows, in their token and with no blur, for their
// reason: what lies under the column is the page the reader was just on.
.drawer-scrim
  position: fixed
  inset: 0
  z-index: $z-drawer-scrim
  background-color: $overlay-bg

// Out of the screen by its own width while it is in, and hidden once the slide
// is over, so its shadow does not show at the edge. It slides back, then
// hides: the visibility waits for the transform.
.drawer-panel
  position: fixed
  top: 0
  bottom: 0
  z-index: $z-drawer
  left: 0
  display: flex
  flex-direction: column
  // Most of the screen, as a menu that slides in from the edge takes it, and
  // the strip of the page left beside it is where a press closes it. A
  // phone lays the page out 706 wide (index.html), and 85% of that is the
  // widest it gets on a tablet too.
  width: min(85vw, 600px)
  // The panel is the frame; .drawer-content below is what scrolls, which is
  // what keeps the close button still.
  overflow: hidden
  box-sizing: border-box
  background-color: $bg-page
  border-right: 1px solid $border
  box-shadow: 0 0 16px $shadow-color
  transform: translateX(-100%)
  visibility: hidden
  transition: transform 0.25s ease, visibility 0s linear 0.25s

  @media (prefers-reduced-motion: reduce)
    transition: none

  &--open
    visibility: visible
    transform: none
    transition: transform 0.25s ease

    @media (prefers-reduced-motion: reduce)
      transition: none

// The bar is the corner a window keeps for its cross: the cross and its inset
// above and below it, so the cross stands in the middle of the bar by
// construction and not by a number of its own. The line under it is the
// theme's, the one between the rows of the column.
//
// What the bar holds starts on the gutter the content of the column stands
// on, and the cross's corner is kept free on the right, so a long line wraps
// before it runs under the cross.
$drawer-bar-height: $dialog-close-size + $dialog-close-inset * 2

.drawer-header
  position: relative
  flex: none
  display: flex
  align-items: center
  min-height: $drawer-bar-height
  padding: 0 $drawer-bar-height 0 $medium
  border-bottom: 1px solid $border

// The name in the heading style of a window.
.drawer-title
  +dialog-title

// The cross of a window, as it is: the same corner, the same two bars.
.drawer-close
  +dialog-close

.drawer-content
  flex: 1 1 auto
  // A flex item will not shrink below its content without this, and then the
  // panel grows instead of scrolling.
  min-height: 0
  overflow-y: auto
  // Scrolled to its end, the column does not hand the scroll on to the page
  // under it.
  overscroll-behavior: contain
  // The first row stands a step under the line, and the content on the same
  // gutter as the name above it.
  padding: $small $medium $big

// The dimming fades in with the timing of ConfirmDialog's backdrop.
.drawer-scrim-enter-active,
.drawer-scrim-leave-active
  transition: opacity 0.2s ease

  @media (prefers-reduced-motion: reduce)
    transition: none

.drawer-scrim-enter-from,
.drawer-scrim-leave-to
  opacity: 0
</style>
