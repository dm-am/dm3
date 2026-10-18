<template>
  <li>
    <h4 class="sidebar-title">
      <span ref="titleSlot"
        ><slot name="title">{{ title }}</slot></span
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
import { computed, ref, onMounted } from "vue";

// The body of a block is a <ul>, so every child a caller hands to the default
// slot has to be an <li> of its own: a bare <div> in there, or a bare
// sentence, is invalid nesting. A wrapper <li> is exactly the box its content
// had — `list-style: none` on .list is inherited, so no marker appears, and
// the global reset zeroes li margin and padding, so the row gains no indent
// and no height of its own. SidebarBlockNesting.spec.ts holds every block in
// this directory to that, this one included.
//
// One heading, two ways in. A block whose title is a constant passes the
// prop alone: it renders as the slot's fallback and names the toggle. A block
// that builds its heading out of loaded data fills the slot instead, and then
// the slot's own text is read once on mount for the aria-label — which is why
// a data-driven heading is better off passing the prop too.
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

// When the +/- shows is the stylesheet's alone, and these rules stand after
// the one above that hides it. It was an inline binding on the hover of the
// title row, and an inline opacity outranks every rule: the one for a touch
// screen below never applied, and a phone had no sign at all.
//
// Under a pointer that hovers, on the whole title row; on keyboard focus of
// the toggle, as the pointer would.
@media (hover: hover)
  .sidebar-title:hover .icon
    opacity: 1

.toggle:focus-visible .icon
  opacity: 1

// A touch screen has no hover: the sign stays faintly visible there, so it
// can be found without lighting up every heading of the column.
@media (pointer: coarse)
  .icon
    opacity: 0.4

.list
  list-style: none
  padding: 6px
  margin: -6px
</style>
