<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from "vue";
import { useUiStore } from "@/shared/stores/ui";
import { useRegion } from "@/shared/lib/composables/useRegion";
import { Theme } from "@/shared/api/models/personal";
import { storeToRefs } from "pinia";
import { SvgIcon } from "@/shared/ui/Icon";
import { Tooltip } from "@/shared/ui/Tooltip";

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
  nextTick(updateViewIndicator);
});

onUnmounted(() => {
  document.removeEventListener("click", closeSettings);
});

// Theme toggle
const uiStore = useUiStore();
const { theme, isCompactMode } = storeToRefs(uiStore);
const { toggleTheme, toggleCompactMode } = uiStore;
const isDarkTheme = computed(() => theme.value === Theme.Dark);

// View toggle indicator
const viewToggleRef = ref<HTMLElement | null>(null);
const viewIndicatorStyle = ref({ left: "0px", width: "50%" });

function updateViewIndicator() {
  if (!viewToggleRef.value) return;
  const btns = viewToggleRef.value.querySelectorAll(".view-toggle-btn");
  const activeIndex = isCompactMode.value ? 0 : 1;
  const activeBtn = btns[activeIndex] as HTMLElement;
  if (!activeBtn) return;
  viewIndicatorStyle.value = {
    left: `${activeBtn.offsetLeft}px`,
    width: `${activeBtn.offsetWidth}px`,
  };
}

watch(isCompactMode, () => {
  nextTick(updateViewIndicator);
});

// Region switcher
const {
  currentRegion,
  canSwitch,
  switchTooltip,
  switchRegion,
  isHydrated,
  isTransferring,
} = useRegion();
</script>

<template>
  <div class="scroll-nav">
    <div class="scroll-buttons">
      <button
        type="button"
        class="scroll-nav-btn"
        aria-label="Прокрутить наверх"
        @click="scrollToTop"
      >
        <SvgIcon name="scrollUpMaterial" />
      </button>
      <button
        type="button"
        class="scroll-nav-btn"
        aria-label="Прокрутить вниз"
        @click="scrollToBottom"
      >
        <SvgIcon name="scrollDownMaterial" />
      </button>
    </div>
    <button
      ref="settingsBtn"
      type="button"
      class="scroll-nav-btn settings-btn"
      :class="{ active: isSettingsOpen }"
      aria-label="Тема и зеркало"
      @click="toggleSettings"
    >
      <SvgIcon name="settingsGear" />
    </button>

    <!-- Settings bubble -->
    <div
      v-if="isSettingsOpen"
      ref="settingsBubble"
      class="settings-bubble"
    >
      <div class="bubble-tail" />
      <div class="bubble-content">
        <!-- View mode toggle -->
        <div class="settings-row">
          <span class="settings-label">Режим</span>
          <div ref="viewToggleRef" class="view-toggle">
            <Tooltip text="Компактный режим">
              <button
                type="button"
                class="view-toggle-btn"
                :class="{ active: isCompactMode }"
                @click="!isCompactMode && toggleCompactMode()"
              >
                <svg viewBox="0 0 16 12" width="16" height="12" fill="currentColor">
                  <rect x="0" y="0" width="16" height="2" />
                  <rect x="0" y="3.33" width="16" height="2" />
                  <rect x="0" y="6.67" width="16" height="2" />
                  <rect x="0" y="10" width="16" height="2" />
                </svg>
              </button>
            </Tooltip>
            <Tooltip text="Обычный режим">
              <button
                type="button"
                class="view-toggle-btn"
                :class="{ active: !isCompactMode }"
                @click="isCompactMode && toggleCompactMode()"
              >
                <svg viewBox="0 0 16 12" width="16" height="12" fill="currentColor">
                  <circle cx="1.5" cy="1.5" r="1.5" />
                  <rect x="5" y="0" width="11" height="2.5" />
                  <circle cx="1.5" cy="6" r="1.5" />
                  <rect x="5" y="4.75" width="11" height="2.5" />
                  <circle cx="1.5" cy="10.5" r="1.5" />
                  <rect x="5" y="9.25" width="11" height="2.5" />
                </svg>
              </button>
            </Tooltip>
            <span class="view-indicator" :style="viewIndicatorStyle"></span>
          </div>
        </div>

        <!-- Theme toggle -->
        <div class="settings-row">
          <span class="settings-label">Тема</span>
          <div class="settings-control">
            <label class="theme-switch">
              <input type="checkbox" :checked="isDarkTheme" @change="toggleTheme" />
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
              :class="{ 'is-loading': isTransferring }"
              :aria-label="switchTooltip"
              :disabled="!canSwitch || isTransferring"
              @click="switchRegion"
            >
              <span v-if="currentRegion?.id === 'main'" class="flag-icon">🇷🇺</span>
              <span v-else class="globe-icon">🌐︎</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/ZIndex"

.scroll-nav
  position: fixed
  right: $medium
  bottom: 12px
  display: flex
  flex-direction: column
  gap: 0
  z-index: $z-sticky

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

.bubble-content
  background: $bg-element
  border: 1px solid $border
  border-radius: $border-radius
  padding: $small $medium
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15)
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
  display: flex
  align-items: center
  gap: $small
  padding: $tiny 0

  &:not(:last-child)
    border-bottom: 1px solid $border
    padding-bottom: $small
    margin-bottom: $tiny

.settings-label
  color: $text-muted
  font-size: $secondary-font-size
  min-width: 55px

// Control wrapper for centering relative to view-toggle
.settings-control
  display: flex
  justify-content: center
  // Match view-toggle width (2 buttons × ~32px each)
  min-width: 64px

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

  .slider
    position: absolute
    top: 0
    left: 0
    right: 0
    bottom: 0
    border-radius: 18px
    background-color: $bg-page
    border: 1px solid $link-nav
    transition: background-color 0.3s ease

    &:before
      content: ''
      position: absolute
      width: 12px
      height: 12px
      left: 2px
      bottom: 2px
      border-radius: 50%
      background: $link-nav
      transition: transform 0.3s ease-in-out, background 0.3s ease

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
      transition: transform 0.3s ease-in-out

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
    cursor: not-allowed
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

// View toggle
.view-toggle
  display: flex
  border-bottom: 2px solid $border
  position: relative

.view-toggle-btn
  display: flex
  align-items: center
  justify-content: center
  padding: $tiny $small
  border: none
  background: none
  color: $text-muted
  cursor: pointer
  transition: filter 0.2s ease

  &:hover:not(.active)
    filter: brightness($hover-brightness)

  &.active
    filter: brightness($hover-brightness)
    cursor: default

.view-indicator
  position: absolute
  bottom: -2px
  height: 2px
  background-color: $text-muted
  transition: left 0.25s ease, width 0.25s ease
  pointer-events: none
</style>
