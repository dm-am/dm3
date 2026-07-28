<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from "vue";
import { useUiStore } from "@/shared/stores/ui";
import { useRegion } from "@/shared/lib/composables/useRegion";
import {
  hasAnyExpandable,
  allExpandablesExpanded,
  expandAllExpandables,
  collapseAllExpandables,
} from "@/shared/lib/composables";
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

function getScrollContainer(): HTMLElement | null {
  return document.querySelector(".main");
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

onMounted(() => {
  document.addEventListener("click", closeSettings);
  nextTick(updateLayoutIndicator);
});

onUnmounted(() => {
  document.removeEventListener("click", closeSettings);
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

// Region switcher
const {
  currentRegion,
  canSwitch,
  switchTooltip,
  switchRegion,
  isHydrated,
  isSwitching,
} = useRegion();
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

        <!-- Region switcher -->
        <div v-if="isHydrated && canSwitch" class="settings-row">
          <span class="settings-label">Зеркало</span>
          <div class="settings-control">
            <button
              type="button"
              class="mirror-btn"
              :class="{ 'is-loading': isSwitching }"
              :aria-label="switchTooltip"
              :disabled="!canSwitch || isSwitching"
              @click="switchRegion"
            >
              <span v-if="currentRegion?.id === 'main'" class="flag-icon"
                >🇷🇺</span
              >
              <span v-else class="globe-icon">🌐︎</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/ZIndex"

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

.scroll-nav-btn
  display: flex
  align-items: center
  justify-content: center
  width: 24px
  height: 24px
  padding: 0
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  cursor: pointer
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
  z-index: 101

// The bubble uses a two-column grid on .bubble-content. Each
// .settings-row inherits those shared column tracks via CSS subgrid,
// so the second column is ALWAYS sized by the widest control across
// ALL rows (the layout toggle, ~64px). The theme switch and mirror
// button center within that exact same column width via
// justify-self: center. No magic numbers — the layout toggle is the
// single source of truth for the controls column width.
.bubble-content
  display: grid
  grid-template-columns: auto auto
  column-gap: $medium
  background: $bg-element
  border: 1px solid $border
  border-radius: $border-radius
  padding: $small $medium
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
  padding: $tiny 0

  &:not(:last-child)
    border-bottom: 1px solid $border
    padding-bottom: $small
    margin-bottom: $tiny

.settings-label
  color: $text-muted
  font-size: $secondary-font-size
  white-space: nowrap

// Theme switch and mirror button: centered within the shared controls
// column (whose width is defined by the layout toggle in the first row).
// justify-self + align-self center the element itself in the grid cell
// without stretching it to fill the cell width.
.settings-control
  justify-self: center
  align-self: center

// Theme switch
.theme-switch
  position: relative
  display: inline-block
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

// Mirror button
.mirror-btn
  display: flex
  align-items: center
  justify-content: center
  width: 36px
  height: 18px
  background: none
  border: none
  padding: 0
  font-size: 1.3em
  line-height: 1
  color: $link-nav
  cursor: pointer

  &:hover:not(:disabled)
    color: $link-nav-hover

  &:disabled
    cursor: default
    opacity: 0.4

  &.is-loading
    animation: pulse 1s infinite

.globe-icon
  position: relative
  top: 0.05em

.flag-icon
  position: relative
  top: -0.1em
  font-size: 1.2em

@keyframes pulse
  0%, 100%
    opacity: 0.7
  50%
    opacity: 0.3

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
