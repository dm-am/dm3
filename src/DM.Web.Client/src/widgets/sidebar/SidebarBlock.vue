<template>
  <div>
    <h4
      class="sidebar-title"
      @mouseenter="hovered = true"
      @mouseleave="hovered = false"
    >
      <slot name="title" /><button
        type="button"
        class="toggle"
        :aria-expanded="show"
        :aria-label="show ? 'Свернуть раздел' : 'Развернуть раздел'"
        @click="toggle"
      >
        <span
          class="icon"
          aria-hidden="true"
          :style="{
            transform: `rotate(${rotation}deg)`,
            opacity: hovered ? 1 : 0,
          }"
        ></span>
      </button>
    </h4>
    <div :class="{ list: true, collapsed: !show }" ref="content">
      <slot />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onUnmounted } from "vue";

const props = defineProps<{ token: string }>();
const storageKey = computed(() => `__HideMenuModule_${props.token}__`);

const show = ref(localStorage.getItem(storageKey.value) !== false.toString());
const content = ref<HTMLElement | null>(null);
const rotation = ref(show.value ? 45 : 0);
const hovered = ref(false);

// Track timers for cleanup on unmount
const timers: ReturnType<typeof setTimeout>[] = [];

function safeTimeout(fn: () => void, delay: number) {
  const id = setTimeout(() => {
    fn();
    const idx = timers.indexOf(id);
    if (idx !== -1) timers.splice(idx, 1);
  }, delay);
  timers.push(id);
}

onUnmounted(() => {
  timers.forEach(clearTimeout);
});

const toggle = () => {
  localStorage.setItem(storageKey.value, (show.value = !show.value).toString());
  rotation.value = show.value ? 45 : 0;
  if (show.value) {
    content.value!.style.height = "auto";
    const expectedHeight = content.value!.clientHeight;
    content.value!.style.height = "0";
    safeTimeout(() => (content.value!.style.height = `${expectedHeight}px`), 0);
    safeTimeout(() => (content.value!.style.height = "auto"), 200);
  } else {
    content.value!.style.height = `${content.value!.clientHeight}px`;
    safeTimeout(() => (content.value!.style.height = "0"), 0);
  }
};
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.sidebar-title
  display: flex
  align-items: center
  margin: $medium 0 $small
  font-size: $font-size
  font-weight: bold
  text-transform: uppercase
  letter-spacing: 0.5px
  color: $heading-alt

// Only the +/- icon toggles the block (the title text is not clickable).
// A small padding gives the icon a comfortable tap target without changing
// its visual size.
.toggle
  cursor: pointer
  color: inherit
  display: inline-flex
  align-items: center
  vertical-align: middle
  padding: 2px 4px
  margin-left: 2px
  border: none
  background: none

  // Keyboard parity with hover: reveal the +/- icon on keyboard focus
  // (!important overrides the inline hover-driven opacity binding)
  &:focus-visible .icon
    opacity: 1 !important

.icon
  position: relative
  display: inline-block
  width: 10px
  height: 10px
  opacity: 0
  transition: opacity 0.15s ease, transform 0.3s ease
  vertical-align: middle

  &::before,
  &::after
    content: ""
    position: absolute
    top: 50%
    left: 50%
    background-color: $heading-alt

  &::before
    // Horizontal line
    width: 10px
    height: 2px
    transform: translate(-50%, -50%)

  &::after
    // Vertical line
    width: 2px
    height: 10px
    transform: translate(-50%, -50%)

.list
  overflow: hidden
  padding: 6px
  margin: -6px
  transition: height 0.3s ease, padding 0.3s ease, margin 0.3s ease
  @media (prefers-reduced-motion: reduce)
    transition: none

  &.collapsed
    height: 0
    padding: 0
    margin: 0
</style>
