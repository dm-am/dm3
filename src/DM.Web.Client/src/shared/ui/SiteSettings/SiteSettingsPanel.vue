<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted } from "vue";
import { storeToRefs } from "pinia";
import { useUiStore } from "@/shared/stores/ui";
import { Theme } from "@/shared/api/models/personal";
import { SvgIcon } from "@/shared/ui/Icon";
import { Tooltip } from "@/shared/ui/Tooltip";

const uiStore = useUiStore();
const { theme, isCompactLayout, hyphenateWords } = storeToRefs(uiStore);
const { toggleTheme, setMessageLayout, setHyphenateWords } = uiStore;
const isDarkTheme = computed(() => theme.value === Theme.Dark);

// A switch, not a pair of buttons: a pair five times its neighbours' width tripled the panel.
const isChecked = (event: Event) => (event.target as HTMLInputElement).checked;

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

watch(isCompactLayout, () => {
  nextTick(updateLayoutIndicator);
});
</script>

<template>
  <div class="settings-panel">
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

    <!-- A row with a switch is a label as a whole: on a phone the switch is a small target at the row's end. -->
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
// Fixed, not auto: the bubble hangs off the right edge, so a wider control moved every row.
// 64 is the layout toggle's own width; settings-panel.spec.ts in e2e fails when a control outgrows it.
$settings-control-width: 64px

.settings-panel
  display: grid
  grid-template-columns: auto $settings-control-width
  column-gap: $medium
  white-space: nowrap

.settings-row
  // Both parent columns through subgrid: every row shares the column widths.
  grid-column: 1 / -1
  display: grid
  grid-template-columns: subgrid
  align-items: center
  // Labels from the left; the controls are centred (.settings-control).
  justify-items: start
  // Equal above and below, so a separator sits midway between its two rows.
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

// Centred under the layout toggle: at the left edge it read as not centred under the control above it.
.settings-control
  align-self: center
  justify-self: center

// The switch of every yes-or-no row; the theme row adds only its moon.
.settings-switch
  position: relative
  // Block: an inline box reserves the descender under itself and made the row taller.
  display: block
  width: 36px
  height: 18px
  cursor: pointer
  flex-shrink: 0

  input
    opacity: 0
    width: 0
    height: 0

  // Colours snap during a theme switch (.no-transitions); only transform and opacity survive it, by !important.
  .slider
    position: absolute
    top: 0
    left: 0
    right: 0
    bottom: 0
    border-radius: 18px
    background-color: $bg-page
    border: 1px solid $link-nav
    transition: background-color $transition-slow, border-color $transition-slow

    &:before
      content: ''
      position: absolute
      width: 12px
      height: 12px
      left: 2px
      bottom: 2px
      border-radius: 50%
      background: $link-nav
      transition: transform $transition-slow !important

  input:checked + .slider:before
    transform: translateX(18px)

  // The input is hidden at no size, so the ring goes round the switch the reader sees.
  input:focus-visible + .slider
    +focus-ring

  // The whole row is the target, so the whole row lights the switch.
  label.settings-row:hover & .slider
    border-color: $link-nav-hover

    &:before
      background: $link-nav-hover

  // The moon: a disc of the page colour over the knob at the dark end.
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
      transition: transform $transition-slow !important

    input:checked + .slider:after
      transform: translateX(18px)
      opacity: 1

// Centred in the column like the switches under it.
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
  transition: filter $transition-normal
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
  transition: left $transition-slow, width $transition-slow
  pointer-events: none
</style>
