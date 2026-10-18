<script setup lang="ts">
/**
 * The settings of the site for this device: the layout of messages, the theme
 * and the hyphenation of posts and comments.
 *
 * The rows of the bubble of the gear in the floating column (ScrollNav), which
 * draws the frame around them, at every width. They stood for a while as the
 * last block of the right column of a phone as well; that column holds no
 * settings now (12 October 2026), and the gear is on a phone again.
 */
import { ref, computed, watch, nextTick, onMounted } from "vue";
import { storeToRefs } from "pinia";
import { useUiStore } from "@/shared/stores/ui";
import { Theme } from "@/shared/api/models/personal";
import { SvgIcon } from "@/shared/ui/Icon";
import { Tooltip } from "@/shared/ui/Tooltip";

const uiStore = useUiStore();
const { theme, messageLayout, isCompactLayout, hyphenateWords } =
  storeToRefs(uiStore);
const { toggleTheme, setMessageLayout, setHyphenateWords } = uiStore;
const isDarkTheme = computed(() => theme.value === Theme.Dark);

// The typographic row takes the switch of the "Тема" row, not the old site's
// "Включить | Выключить" pair of buttons: a row of this panel is a label and a
// small control, and a pair of buttons five times its neighbours' width tripled
// the panel and read as a form dropped into it.
//
// Justification has no row. The owner took the switch away: posts and
// comments are always set flush on both sides.
const isChecked = (event: Event) => (event.target as HTMLInputElement).checked;

// Layout toggle indicator
const layoutToggleRef = ref<HTMLElement | null>(null);
const layoutIndicatorStyle = ref({ left: "0px", width: "50%" });

function updateLayoutIndicator() {
  if (!layoutToggleRef.value) return;
  const btns = layoutToggleRef.value.querySelectorAll(".layout-toggle-btn");
  const activeIndex = isCompactLayout.value ? 0 : 1;
  const activeBtn = btns[activeIndex] as HTMLElement;
  if (!activeBtn) return;
  layoutIndicatorStyle.value = {
    left: `${activeBtn.offsetLeft}px`,
    width: `${activeBtn.offsetWidth}px`,
  };
}

onMounted(() => nextTick(updateLayoutIndicator));

watch(messageLayout, () => {
  nextTick(updateLayoutIndicator);
});
</script>

<template>
  <div class="settings-panel">
    <!-- Message layout toggle -->
    <div class="settings-row">
      <span class="settings-label">Верстка</span>
      <div ref="layoutToggleRef" class="layout-toggle">
        <Tooltip text="Компактная">
          <button
            type="button"
            class="layout-toggle-btn"
            :class="{ active: isCompactLayout }"
            aria-label="Компактная верстка сообщений"
            @click="setMessageLayout('compact')"
          >
            <SvgIcon name="layoutCompact" />
          </button>
        </Tooltip>
        <Tooltip text="Полная">
          <button
            type="button"
            class="layout-toggle-btn"
            :class="{ active: !isCompactLayout }"
            aria-label="Полная верстка сообщений"
            @click="setMessageLayout('full')"
          >
            <SvgIcon name="layoutList" />
          </button>
        </Tooltip>
        <span class="layout-indicator" :style="layoutIndicatorStyle"></span>
      </div>
    </div>

    <!-- The address of the site is not a setting and is not here. A control
         in this panel is read only by someone whose page has loaded, which
         is exactly the visitor for whom the other address changes nothing.
         The one who needs it cannot open the panel at all, so the address
         lives where it reaches him: in the footer of the site and in the
         footer of every letter. -->

    <!-- A row with a switch is a label as a whole: a press anywhere on it
         turns the switch, not only a press on the switch, which on a phone
         is a small target at the far end of a wide row. -->
    <label class="settings-row">
      <span class="settings-label">Тема</span>
      <span class="settings-control">
        <span class="settings-switch settings-switch--theme">
          <input
            type="checkbox"
            :checked="isDarkTheme"
            aria-label="Темная тема"
            @change="toggleTheme"
          />
          <span class="slider" />
        </span>
      </span>
    </label>

    <!-- Hyphenation of posts and comments -->
    <label class="settings-row">
      <span class="settings-label">Перенос слов</span>
      <span class="settings-control">
        <span class="settings-switch">
          <input
            type="checkbox"
            :checked="hyphenateWords"
            aria-label="Перенос слов"
            @change="setHyphenateWords(isChecked($event))"
          />
          <span class="slider" />
        </span>
      </span>
    </label>
  </div>
</template>

<style scoped lang="sass">
// Width of the column every control sits in, declared once and fixed. The
// panel is a two-column grid, and each .settings-row inherits those tracks
// through subgrid.
//
// It used to be "auto", which meant the widest control decided it. That is a
// coupling nobody can see: the bubble of the gear hangs off the right edge, so
// a control two pixels wider pushed the whole panel left, moved every label
// with it and re-centred the controls of the other rows. One segmented switch,
// added to a row that has since been removed again, shifted every row in the
// panel.
//
// 64 is the intrinsic width of the layout toggle, the widest control the panel
// has, measured rather than guessed. Every other row holds the 36px switch of
// the "Тема" row, centred under the layout toggle. Anything wider is a design
// decision to take deliberately, by raising this number: a "Включить |
// Выключить" pair of buttons once made it 200 and the panel four hundred
// pixels wide.
// e2e/tests/common/settings-panel.spec.ts fails when a control outgrows it, so
// the coupling cannot come back unnoticed.
//
// The label track is auto: in the bubble it is as wide as the longest label.
$settings-control-width: 64px

.settings-panel
  display: grid
  grid-template-columns: auto $settings-control-width
  column-gap: $medium
  white-space: nowrap

.settings-row
  // Span both parent columns and inherit their track sizing via subgrid.
  // This is what makes all rows share the same column widths.
  grid-column: 1 / -1
  display: grid
  grid-template-columns: subgrid
  align-items: center
  // Labels from the left edge of their column. The controls are not: see
  // .settings-control.
  justify-items: start
  // Equal above and below, so a separator sits midway between the two rows it
  // divides. It used to be 2 above and 8 below with 2 more of margin, which
  // pinned every row to the line above it and left twice the air under it.
  padding: $small 0

  &:not(:last-child)
    border-bottom: 1px solid $border

// A row that is a label turns its switch from anywhere on it.
label.settings-row
  cursor: pointer

.settings-label
  color: $text-muted
  font-size: $secondary-font-size
  white-space: nowrap

// Switch wrapper: in the middle of the controls column, so the centre of every
// switch stands under the centre of the layout toggle, which fills that
// column; and in the middle of the height of the row, without stretching to
// fill the cell. The owner's word: a switch at the left edge of the column
// read as not centred under the control above it.
.settings-control
  align-self: center
  justify-self: center

// The switch of every yes-or-no row: "Тема" and "Перенос слов". One control,
// one size; the theme row adds only its moon.
.settings-switch
  position: relative
  // block, not inline-block: an inline box sits on the text baseline and
  // reserves the descender space under itself, which made this row two and a
  // half pixels taller than the two around it for no visible reason.
  display: block
  width: 36px
  height: 18px
  cursor: pointer
  flex-shrink: 0

  input
    opacity: 0
    width: 0
    height: 0

  // Colors snap instantly during theme switch (.no-transitions handles this).
  // Only transform/opacity use !important to survive .no-transitions —
  // scoped specificity (0,0,2,1) beats global .no-transitions (0,0,1,2).
  // cubic-bezier(0.4, 0, 0.2, 1) — Material standard easing (natural deceleration).
  .slider
    position: absolute
    top: 0
    left: 0
    right: 0
    bottom: 0
    border-radius: 18px
    background-color: $bg-page
    border: 1px solid $link-nav
    transition: background-color 0.3s ease, border-color 0.3s ease

    &:before
      content: ''
      position: absolute
      width: 12px
      height: 12px
      left: 2px
      bottom: 2px
      border-radius: 50%
      background: $link-nav
      transition: transform 0.4s cubic-bezier(0.4, 0, 0.2, 1) !important

  input:checked + .slider:before
    transform: translateX(18px)

  // The input is the control and takes the caret, but it is hidden at no
  // size, so the ring the site draws around a focused control would go round
  // nothing. It goes round the switch the reader sees, as on every block
  // control (UI_STANDARDS, "Клавиатура и фокус"); the mouse draws none.
  input:focus-visible + .slider
    outline: 2px solid $link
    outline-offset: 0

  // The whole row is the target, so the whole row lights the switch.
  label.settings-row:hover & .slider
    border-color: $link-nav-hover

    &:before
      background: $link-nav-hover

  // The moon of the dark theme: a disc of the page colour over the knob, shown
  // when the knob stands at the dark end, turns it into a crescent. It belongs
  // to the theme and not to switching, so the other rows keep a plain knob.
  &--theme
    .slider:after
      content: ''
      position: absolute
      width: 9px
      height: 9px
      left: 0
      bottom: 4px
      border-radius: 50%
      background: $bg-page
      z-index: 1
      opacity: 0
      transition: transform 0.4s cubic-bezier(0.4, 0, 0.2, 1) !important

    input:checked + .slider:after
      transform: translateX(18px)
      opacity: 1

// Layout toggle. Centred in the column like the switches under it, so the two
// centres meet whatever the toggle measures; today it fills the column.
.layout-toggle
  justify-self: center
  display: flex
  border-bottom: 2px solid $border
  position: relative

.layout-toggle-btn
  display: flex
  align-items: center
  justify-content: center
  padding: $tiny $small
  border: none
  background: none
  color: $text-muted
  cursor: pointer
  transition: filter 0.2s ease
  svg
    width: 16px
    height: 12px

  &:hover:not(.active)
    filter: brightness($hover-brightness)

  &.active
    filter: brightness($hover-brightness)
    cursor: default

.layout-indicator
  position: absolute
  bottom: -2px
  height: 2px
  background-color: $text-muted
  transition: left 0.25s ease, width 0.25s ease
  pointer-events: none
</style>
