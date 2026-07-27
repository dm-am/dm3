<template>
  <li>
    <h4
      class="sidebar-title"
      @mouseenter="hovered = true"
      @mouseleave="hovered = false"
    >
      <span ref="titleSlot"><slot name="title" /></span
      ><button
        :id="toggleId"
        type="button"
        class="toggle"
        :aria-expanded="show"
        :aria-controls="listId"
        :aria-label="toggleLabel"
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
    <div class="expand-fold" :class="{ open: show }">
      <div class="expand-fold-clip" :inert="!show">
        <ul :id="listId" class="list">
          <slot />
        </ul>
      </div>
    </div>
  </li>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from "vue";

// `title` is optional — most callers already render the block heading via
// the #title slot, so when the prop is omitted the slot's own text content
// is read once on mount and used for the aria-label instead.
const props = defineProps<{ token: string; title?: string }>();
const storageKey = computed(() => `__HideMenuModule_${props.token}__`);
const listId = computed(() => `sidebar-list-${props.token}`);
const toggleId = computed(() => `sidebar-toggle-${props.token}`);

const titleSlot = ref<HTMLElement | null>(null);
const slotTitleText = ref("");
onMounted(() => {
  if (!props.title) {
    slotTitleText.value = titleSlot.value?.textContent?.trim() ?? "";
  }
});
const resolvedTitle = computed(() => props.title ?? slotTitleText.value);

const show = ref(localStorage.getItem(storageKey.value) !== false.toString());
const rotation = ref(show.value ? 45 : 0);
const hovered = ref(false);

// Computed in script rather than inline in the template: an inline template
// literal cannot carry straight quotes around the title (v-bind does not
// decode &quot; entities, and the attribute delimiter forbids raw quotes).
const toggleLabel = computed(() =>
  show.value
    ? `Свернуть раздел "${resolvedTitle.value}"`
    : `Развернуть раздел "${resolvedTitle.value}"`,
);

// The reveal itself is the global CSS-only .expand-fold (Reset.sass) —
// persistent sidebar sections stay OUT of the expandable registry but
// share the unified animation tempo (UI_STANDARDS, Animation Standards).
const toggle = () => {
  localStorage.setItem(storageKey.value, (show.value = !show.value).toString());
  rotation.value = show.value ? 45 : 0;
};
</script>

<style scoped lang="sass">
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
// Padding gives the icon a ~24px tap target without changing its visual size.
.toggle
  cursor: pointer
  color: inherit
  display: inline-flex
  align-items: center
  justify-content: center
  vertical-align: middle
  width: 24px
  height: 24px
  margin-left: 2px
  border: none
  background: none

  // Keyboard parity with hover: reveal the +/- icon on keyboard focus
  // (!important overrides the inline hover-driven opacity binding)
  &:focus-visible .icon
    opacity: 1 !important

  // Touch devices have no hover — keep the affordance faintly visible so
  // it can be discovered without accidental taps everywhere.
  @media (pointer: coarse)
    .icon
      opacity: 0.4

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
  list-style: none
  padding: 6px
  margin: -6px
</style>
