<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from "vue";
import { useUiStore } from "@/shared/stores/ui";
import {
  hasAnyExpandable,
  allExpandablesExpanded,
  expandAllExpandables,
  collapseAllExpandables,
} from "@/shared/lib/composables";
import { getScrollContainer } from "@/shared/lib/scroll";
import { Theme } from "@/shared/api/models/personal";
import { storeToRefs } from "pinia";
import { SvgIcon } from "@/shared/ui/Icon";
import { Tooltip } from "@/shared/ui/Tooltip";

function toggleAllExpandables() {
  if (allExpandablesExpanded.value) {
    collapseAllExpandables();
  } else {
    expandAllExpandables();
  }
}

function scrollToTop() {
  const container = getScrollContainer();
  if (container) {
    container.scrollTo({ top: 0, behavior: "smooth" });
  }
}

function scrollToBottom() {
  const container = getScrollContainer();
  if (container) {
    container.scrollTo({ top: container.scrollHeight, behavior: "smooth" });
  }
}

// Settings bubble
const isSettingsOpen = ref(false);
const settingsBtn = ref<HTMLElement | null>(null);
const settingsBubble = ref<HTMLElement | null>(null);

function toggleSettings() {
  isSettingsOpen.value = !isSettingsOpen.value;
}

function closeSettings(e: MouseEvent) {
  if (
    isSettingsOpen.value &&
    settingsBtn.value &&
    settingsBubble.value &&
    !settingsBtn.value.contains(e.target as Node) &&
    !settingsBubble.value.contains(e.target as Node)
  ) {
    isSettingsOpen.value = false;
  }
}

/**
 * Escape closes the bubble and gives the caret back to the button that opened
 * it — the same exit the site's menus have. Only the mouse had one here: the
 * bubble closed on an outside click and on nothing else.
 *
 * The listener is on the document, symmetrical with the outside-click one
 * above, because the bubble holds real controls and the press has to be heard
 * wherever focus sits inside it.
 *
 * The button says `aria-expanded` and deliberately not `aria-haspopup`: this is
 * a disclosure, a panel of settings, not a menu of commands. Announcing a menu
 * here would promise a reader arrow-key navigation between items that are a
 * segmented control and a switch.
 */
function closeSettingsOnEscape(e: KeyboardEvent) {
  if (e.key !== "Escape" || !isSettingsOpen.value) return;
  isSettingsOpen.value = false;
  settingsBtn.value?.focus();
}

onMounted(() => {
  document.addEventListener("click", closeSettings);
  document.addEventListener("keydown", closeSettingsOnEscape);
  nextTick(updateLayoutIndicator);
});

onUnmounted(() => {
  document.removeEventListener("click", closeSettings);
  document.removeEventListener("keydown", closeSettingsOnEscape);
});

// Theme toggle
const uiStore = useUiStore();
const { theme, messageLayout, isCompactLayout } = storeToRefs(uiStore);
const { toggleTheme, setMessageLayout } = uiStore;
const isDarkTheme = computed(() => theme.value === Theme.Dark);

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

watch(messageLayout, () => {
  nextTick(updateLayoutIndicator);
});
</script>

<template>
  <div class="scroll-nav">
    <!-- Toggle all expandables (accordion rows, TruncatedContent, BBCode spoiler/nsfw) -->
    <Tooltip
      v-if="hasAnyExpandable"
      :text="allExpandablesExpanded ? 'Свернуть все' : 'Развернуть все'"
    >
      <button
        type="button"
        class="scroll-nav-btn toggle-all-btn"
        :aria-label="allExpandablesExpanded ? 'Свернуть все' : 'Развернуть все'"
        @click="toggleAllExpandables"
      >
        <SvgIcon :name="allExpandablesExpanded ? 'collapseAll' : 'expandAll'" />
      </button>
    </Tooltip>
    <div class="scroll-buttons">
      <Tooltip text="Наверх">
        <button
          type="button"
          class="scroll-nav-btn"
          aria-label="Прокрутить наверх"
          @click="scrollToTop"
        >
          <SvgIcon name="chevronUp" />
        </button>
      </Tooltip>
      <Tooltip text="Вниз">
        <button
          type="button"
          class="scroll-nav-btn"
          aria-label="Прокрутить вниз"
          @click="scrollToBottom"
        >
          <SvgIcon name="chevronDown" />
        </button>
      </Tooltip>
    </div>
    <Tooltip text="Настройки сайта" :disabled="isSettingsOpen">
      <button
        ref="settingsBtn"
        type="button"
        class="scroll-nav-btn settings-btn"
        :class="{ active: isSettingsOpen }"
        aria-label="Настройки сайта"
        :aria-expanded="isSettingsOpen"
        @click="toggleSettings"
      >
        <SvgIcon name="settings" />
      </button>
    </Tooltip>

    <!-- Settings bubble -->
    <div v-if="isSettingsOpen" ref="settingsBubble" class="settings-bubble">
      <div class="bubble-tail" />
      <div class="bubble-content">
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

        <!-- Theme toggle -->
        <div class="settings-row">
          <span class="settings-label">Тема</span>
          <div class="settings-control">
            <label class="theme-switch">
              <input
                type="checkbox"
                :checked="isDarkTheme"
                aria-label="Темная тема"
                @change="toggleTheme"
              />
              <span class="slider" />
            </label>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/ZIndex"
@import "@/assets/styles/Inputs"

.scroll-nav
  position: fixed
  right: $medium
  bottom: 12px
  display: flex
  flex-direction: column
  gap: 0
  z-index: $z-sticky

.toggle-all-btn
  margin-bottom: $small

.scroll-buttons
  display: flex
  flex-direction: column
  gap: $tiny

.settings-btn
  margin-top: $small

// The hit box comes from the mixin rather than from these 24 pixels. At this
// size the floor and the box coincide, so nothing moves on screen, but the
// number stops being this file's own opinion about how small a target may get.
// The mixin resets border and background, so the frame and the fill that make
// this control look like a button come after it.
.scroll-nav-btn
  +icon-button(24px)

  // Wrapped in "&" because the mixin ends with a nested rule, and a plain
  // declaration after one is what Sass is changing the meaning of. Moving them
  // above the mixin is not the fix here: it resets border and background, so
  // the frame and the fill have to come after it and win.
  &
    border: 1px solid $border
    border-radius: $border-radius
    background-color: $bg-element
    color: $text-muted

  &:hover,
  &.active
    color: $text
    background-color: $bg-element-accent

  svg
    width: 16px
    height: 16px

// Settings bubble
.settings-bubble
  position: absolute
  bottom: 0
  right: calc(100% + $small)
  // A popover, and the scale has a tier for one. The literal 101 was a step
  // above $z-dropdown, so the bubble covered any filter dropdown that reached
  // it — a number picked to win an argument the scale had already settled.
  z-index: $z-popover

// Width of the column every control sits in, declared once and fixed. The
// bubble is a two-column grid on .bubble-content, and each .settings-row
// inherits those tracks through subgrid.
//
// It used to be "auto", which meant the widest control decided it. That is a
// coupling nobody can see: the bubble hangs off the right edge, so a control
// two pixels wider pushed the whole panel left, moved every label with it and
// re-centred the controls of the other rows. One segmented switch, added to a
// row that has since been removed again, shifted every row in the panel.
//
// 64 is the intrinsic width of the layout toggle, the widest control the panel
// has ever had, measured rather than guessed. Anything narrower is centred in
// it; anything wider is a design decision to take deliberately, by raising
// this number. e2e/tests/common/settings-panel.spec.ts fails when a control
// outgrows it, so the coupling cannot come back unnoticed.
$settings-control-width: 64px

.bubble-content
  display: grid
  grid-template-columns: auto $settings-control-width
  column-gap: $medium
  background: $bg-element
  border: 1px solid $border
  border-radius: $border-radius
  // No vertical padding of its own: the rows carry theirs, so the space above
  // the first row, between every pair and below the last is the same one
  // number and stays equal when a row is added or removed.
  padding: 0 $medium
  box-shadow: 0 2px 8px $shadow-color
  white-space: nowrap

.bubble-tail
  position: absolute
  bottom: 6px
  right: -6px
  width: 10px
  height: 10px
  background: $bg-element
  border-right: 1px solid $border
  border-top: 1px solid $border
  transform: rotate(45deg)

.settings-row
  // Span both parent columns and inherit their track sizing via subgrid.
  // This is what makes all rows share the same column widths.
  grid-column: 1 / -1
  display: grid
  grid-template-columns: subgrid
  align-items: center
  // Equal above and below, so a separator sits midway between the two rows it
  // divides. It used to be 2 above and 8 below with 2 more of margin, which
  // pinned every row to the line above it and left twice the air under it.
  padding: $small 0

  &:not(:last-child)
    border-bottom: 1px solid $border

.settings-label
  color: $text-muted
  font-size: $secondary-font-size
  white-space: nowrap

// Theme switch: centered within the shared controls column (whose
// width is defined by the layout toggle in the first row).
// justify-self + align-self center the element itself in the grid cell
// without stretching it to fill the cell width.
.settings-control
  justify-self: center
  align-self: center

// Theme switch
.theme-switch
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

    &:after
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

  input:checked + .slider:before,
  input:checked + .slider:after
    transform: translateX(18px)

  input:checked + .slider:after
    opacity: 1

  &:hover .slider
    border-color: $link-nav-hover

    &:before
      background: $link-nav-hover

// Layout toggle
.layout-toggle
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
