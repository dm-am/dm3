<template>
  <li>
    <h4 class="sidebar-title">
      <span>{{ title }}</span
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
          :style="{ transform: `rotate(${rotation}deg)` }"
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
import { computed, ref } from "vue";

// The body is a <ul>: every child of the default slot is an <li> (SidebarBlockNesting.spec).
const props = defineProps<{ token: string; title: string }>();
const storageKey = computed(() => `__HideMenuModule_${props.token}__`);
const listId = computed(() => `sidebar-list-${props.token}`);
const toggleId = computed(() => `sidebar-toggle-${props.token}`);

const show = ref(localStorage.getItem(storageKey.value) !== "false");
const rotation = computed(() => (show.value ? 45 : 0));

// In script: an attribute cannot carry straight quotes around the title.
const toggleLabel = computed(() =>
  show.value
    ? `Свернуть раздел "${props.title}"`
    : `Развернуть раздел "${props.title}"`,
);

// The global .expand-fold, out of the expandable registry: a block stays as the reader left it.
const toggle = () => {
  show.value = !show.value;
  localStorage.setItem(storageKey.value, String(show.value));
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

// Only the +/- toggles the block, in a tap target of its own.
.toggle
  cursor: pointer
  color: inherit
  display: inline-flex
  align-items: center
  justify-content: center
  vertical-align: middle
  width: $tap-target-min
  height: $tap-target-min
  margin-left: $tiny
  border: none
  background: none

.icon
  position: relative
  display: inline-block
  width: 10px
  height: 10px
  opacity: 0
  transition: opacity $transition-normal, transform $transition-slow
  vertical-align: middle

  &::before,
  &::after
    content: ""
    position: absolute
    top: 50%
    left: 50%
    background-color: $heading-alt

  &::before
    width: 10px
    height: 2px
    transform: translate(-50%, -50%)

  &::after
    width: 2px
    height: 10px
    transform: translate(-50%, -50%)

// The stylesheet alone shows the +/-: on hover of the title row, on keyboard focus.
.sidebar-title:hover .icon
  opacity: 1

.toggle:focus-visible .icon
  opacity: 1

.list
  list-style: none
  padding: $small
  margin: -$small
</style>
