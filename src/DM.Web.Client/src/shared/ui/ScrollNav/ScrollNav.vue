<script setup lang="ts">
import { ref, nextTick, onMounted, onBeforeUnmount } from "vue";
import {
  hasAny as hasAnyExpandable,
  allExpanded as allExpandablesExpanded,
  expandAll as expandAllExpandables,
  collapseAll as collapseAllExpandables,
} from "@/shared/lib/composables/useExpandableRegistry";
import { getScrollContainer } from "@/shared/lib/scroll";
import { SvgIcon } from "@/shared/ui/Icon";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SiteSettingsPanel } from "@/shared/ui/SiteSettings";

/*
 * The column stands at every width and at every moment, with the gear in it,
 * as it stood before the wave of 5 October. That wave made it fade a moment
 * after every scroll on a narrow screen, took it away while a field held the
 * caret or a side column was out, and moved the settings into the right
 * column. The owner, 12 October 2026: the buttons are broken and keep
 * disappearing. Nothing of its own takes it away now; a side column slid out
 * lays its scrim over it and holds the caret itself (MobileDrawer).
 */

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

// Settings bubble, at every width: the rows of SiteSettingsPanel in a frame
// of the bubble's own.
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

/*
 * The width of the scrollbar of the page. Below the shell breakpoint the
 * column stands in the gutter of the page, four from its edge (the style
 * below), and the edge of the page is where its scrollbar begins: none on a
 * phone, where the bar lies over the page, some fifteen pixels on a desktop
 * window narrowed below the breakpoint. Four from the edge of the window would
 * lay the column on that bar. The bar is drawn by the scroll container, so it
 * is measured there, and again on a resize, which a zoom of the page is too.
 */
const pageScrollbar = ref(0);

function measurePageScrollbar() {
  const container = getScrollContainer();
  pageScrollbar.value = container
    ? Math.max(0, container.offsetWidth - container.clientWidth)
    : 0;
}

onMounted(async () => {
  document.addEventListener("click", closeSettings);
  document.addEventListener("keydown", closeSettingsOnEscape);
  window.addEventListener("resize", measurePageScrollbar);
  // The shell registers its scroll container in its own mounted hook, which
  // runs after this one: the container is there once the render has settled.
  await nextTick();
  measurePageScrollbar();
});

onBeforeUnmount(() => {
  document.removeEventListener("click", closeSettings);
  document.removeEventListener("keydown", closeSettingsOnEscape);
  window.removeEventListener("resize", measurePageScrollbar);
});
</script>

<template>
  <div class="scroll-nav" :style="{ '--page-scrollbar': `${pageScrollbar}px` }">
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
      <SiteSettingsPanel class="bubble-content" />
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/Inputs" as *

.scroll-nav
  position: fixed
  right: $medium
  bottom: 12px
  display: flex
  flex-direction: column
  gap: 0
  z-index: $z-sticky

  // Below the shell breakpoint the page has a gutter of $big at its right
  // edge and no column beside it. At $medium from the edge the column lay on
  // the last 8 pixels of every card; at $minor it stands in the gutter, clear
  // of the text by four, and beside the scrollbar of the page where one is
  // drawn.
  @media (max-width: $bp-shell)
    right: calc($minor + var(--page-scrollbar, 0px))

.toggle-all-btn
  margin-bottom: $small

.scroll-buttons
  display: flex
  flex-direction: column
  gap: $tiny

.settings-btn
  margin-top: $small

// The hit box comes from the mixin rather than from the glyph box. At this
// size the floor and the box coincide, so nothing moves on screen, but the
// number stops being this file's own opinion about how small a target may get.
// The mixin resets border and background, so the frame and the fill that make
// this control look like a button come after it.
//
// The width is the shared $scroll-nav-width, because the rail is a landmark
// other fixed corners measure against (ToastContainer).
.scroll-nav-btn
  +icon-button($scroll-nav-width)

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

// The frame of the bubble around the rows of SiteSettingsPanel, which draws
// none of its own.
.bubble-content
  background: $bg-element
  border: 1px solid $border
  border-radius: $border-radius
  // No vertical padding of its own: the rows carry theirs, so the space above
  // the first row, between every pair and below the last is the same one
  // number and stays equal when a row is added or removed.
  padding: 0 $medium
  box-shadow: 0 2px 8px $shadow-color

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
</style>
