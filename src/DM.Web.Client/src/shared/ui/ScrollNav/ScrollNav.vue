<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from "vue";
import {
  hasAny,
  allExpanded,
  expandAll,
  collapseAll,
} from "@/shared/lib/composables/useExpandableRegistry";
import { getScrollContainer } from "@/shared/lib/scroll";
import { SvgIcon } from "@/shared/ui/Icon";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SiteSettingsPanel } from "@/shared/ui/SiteSettings";

function toggleAllExpandables() {
  if (allExpanded.value) {
    collapseAll();
  } else {
    expandAll();
  }
}

function scrollTo(top: (container: HTMLElement) => number) {
  const container = getScrollContainer();
  container?.scrollTo({ top: top(container), behavior: "smooth" });
}

// The rows of SiteSettingsPanel in a frame of the bubble's own.
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

/** Escape gives the caret back to the button. aria-expanded, not aria-haspopup: a disclosure, not a menu. */
function closeSettingsOnEscape(e: KeyboardEvent) {
  if (e.key !== "Escape" || !isSettingsOpen.value) return;
  isSettingsOpen.value = false;
  settingsBtn.value?.focus();
}

onMounted(() => {
  document.addEventListener("click", closeSettings);
  document.addEventListener("keydown", closeSettingsOnEscape);
});

onBeforeUnmount(() => {
  document.removeEventListener("click", closeSettings);
  document.removeEventListener("keydown", closeSettingsOnEscape);
});
</script>

<template>
  <div class="scroll-nav">
    <!-- Accordion rows, TruncatedContent, spoilers and nsfw. -->
    <Tooltip
      v-if="hasAny"
      :text="allExpanded ? 'Свернуть все' : 'Развернуть все'"
    >
      <button
        type="button"
        class="scroll-nav-btn toggle-all-btn"
        :aria-label="allExpanded ? 'Свернуть все' : 'Развернуть все'"
        @click="toggleAllExpandables"
      >
        <SvgIcon :name="allExpanded ? 'collapseAll' : 'expandAll'" />
      </button>
    </Tooltip>
    <div class="scroll-buttons">
      <Tooltip text="Наверх">
        <button
          type="button"
          class="scroll-nav-btn"
          aria-label="Прокрутить наверх"
          @click="scrollTo(() => 0)"
        >
          <SvgIcon name="chevronUp" />
        </button>
      </Tooltip>
      <Tooltip text="Вниз">
        <button
          type="button"
          class="scroll-nav-btn"
          aria-label="Прокрутить вниз"
          @click="scrollTo((container) => container.scrollHeight)"
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

    <div v-if="isSettingsOpen" ref="settingsBubble" class="settings-bubble">
      <div class="bubble-tail" />
      <SiteSettingsPanel class="bubble-content" />
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/Buttons" as *

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

// The hit box comes from the mixin; the width is shared, since other fixed corners measure the rail (ToastContainer).
.scroll-nav-btn
  +icon-button($scroll-nav-width)

  // In "&" after the mixin's nested rule; after the mixin's reset, so the frame and fill win.
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

.settings-bubble
  position: absolute
  bottom: 0
  right: calc(100% + $small)
  // A popover, at the scale's tier for one.
  z-index: $z-popover

.bubble-content
  background: $bg-element
  border: 1px solid $border
  border-radius: $border-radius
  // No vertical padding: the rows carry theirs, so the spacing stays one number.
  padding: 0 $medium
  box-shadow: $shadow-small

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
