<script setup lang="ts">
/**
 * MobileDrawer — generic left-anchored off-canvas drawer (scrim + sliding
 * panel), used for the mobile burger navigation menu (product doc: mobile
 * shell, Variant A). Self-rolled (Teleport + Transition) rather than
 * vue-final-modal — the panel must lock scroll on `.main` (the app's actual
 * scroll container; `body` is always `overflow: hidden`, see Reset.sass),
 * which vue-final-modal's `lock-scroll` cannot target.
 *
 * Mirrors the ConfirmDialog/InputDialog shell conventions: Teleport to body,
 * v-if + Transition, Escape-to-close, backdrop-click-to-close, a minimal
 * focus trap, and focus restore to the trigger on close.
 */
import { ref, computed, watch, onBeforeUnmount } from "vue";
import { useDialogShell } from "@/shared/lib/composables/useDialogShell";
import { getScrollContainer } from "@/shared/lib/scroll";
import { symbols } from "@/shared/lib/utils/icons";

const props = withDefaults(
  defineProps<{
    modelValue: boolean;
    /** Accessible name for the dialog region (drawer has no visible title). */
    ariaLabel?: string;
  }>(),
  {
    ariaLabel: "Меню навигации",
  },
);

const emit = defineEmits<{
  (e: "update:modelValue", value: boolean): void;
}>();

const panel = ref<HTMLElement | null>(null);
const closeBtn = ref<HTMLButtonElement | null>(null);
let lockedScrollEl: HTMLElement | null = null;
let lockedScrollPrevOverflow = "";

function close() {
  emit("update:modelValue", false);
}

function lockScroll() {
  // The app's real scroll container is App.vue's `.main`, published through
  // the scroll registry — `body` is permanently `overflow: hidden`
  // (Reset.sass), so locking body has no effect here.
  lockedScrollEl = getScrollContainer();
  if (lockedScrollEl) {
    lockedScrollPrevOverflow = lockedScrollEl.style.overflow;
    lockedScrollEl.style.overflow = "hidden";
  }
}

function unlockScroll() {
  if (lockedScrollEl) {
    lockedScrollEl.style.overflow = lockedScrollPrevOverflow;
    lockedScrollEl = null;
  }
}

// The drawer is a modal, so it owes a reader what every modal owes: focus in
// on open, trapped while open, back where it was on close, Escape closes. That
// behaviour is useDialogShell's, and this file used to carry a second copy of
// it — its own focusables() included — which is how two copies drift.
const shell = useDialogShell({
  show: computed(() => props.modelValue),
  container: panel,
  initialFocus: () => closeBtn.value,
  onDismiss: close,
});

// The scroll lock stays here: the shell knows about focus, not about the page
// behind it.
watch(
  () => props.modelValue,
  (open) => (open ? lockScroll() : unlockScroll()),
  // Also handles the (unusual but possible) case of the drawer being
  // mounted already open — locks scroll immediately instead of only on
  // the next open/close transition.
  { immediate: true },
);

onBeforeUnmount(() => {
  unlockScroll();
});
</script>

<template>
  <Teleport to="body">
    <!-- Single Transition on the scrim root; the panel's slide is driven by
         descendant selectors below (the standard Vue "modal" nested-animation
         pattern) instead of a second nested <Transition> component. -->
    <Transition name="drawer">
      <div
        v-if="modelValue"
        class="drawer-scrim"
        @click="shell.handleBackdropClick"
        @keydown="shell.handleKeydown"
      >
        <!-- tabindex="-1": the keydown listener is on the scrim, and a click on
             the panel's own text drops focus to <body>, which is the scrim's
             ancestor — the event would never reach the listener and Escape
             would stop closing the drawer. -->
        <div
          ref="panel"
          class="drawer-panel"
          tabindex="-1"
          role="dialog"
          aria-modal="true"
          :aria-label="ariaLabel"
        >
          <button
            ref="closeBtn"
            type="button"
            class="drawer-close"
            aria-label="Закрыть меню"
            @click="close"
          >
            {{ symbols.close }}
          </button>
          <div class="drawer-content">
            <slot />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *

.drawer-scrim
  position: fixed
  inset: 0
  z-index: $z-drawer-scrim
  background-color: $overlay-bg
  backdrop-filter: blur(2px)

.drawer-panel
  position: fixed
  inset: 0 auto 0 0
  z-index: $z-drawer
  width: min(85vw, 360px)
  height: 100%
  overflow-y: auto
  box-sizing: border-box
  background-color: $bg-page
  border-right: 1px solid $border
  box-shadow: 4px 0 16px $shadow-color
  padding: $big $medium
  // Room for the close button pinned top-right without overlapping content
  padding-top: 56px

.drawer-close
  position: absolute
  top: $small
  right: $small
  display: flex
  align-items: center
  justify-content: center
  width: 44px
  height: 44px
  border: none
  background: none
  font-size: 22px
  line-height: 1
  cursor: pointer
  color: $text-muted

  &:hover
    color: $text

// Scrim fade (same timing as ConfirmDialog/InputDialog's backdrop) + panel
// slide, driven off the SAME enter/leave classes via descendant selectors —
// the standard Vue nested-transition trick, no second <Transition> needed.
.drawer-enter-active,
.drawer-leave-active
  transition: opacity 0.2s ease
  @media (prefers-reduced-motion: reduce)
    transition: none

  .drawer-panel
    transition: transform 0.25s ease
    @media (prefers-reduced-motion: reduce)
      transition: none

.drawer-enter-from,
.drawer-leave-to
  opacity: 0

  .drawer-panel
    transform: translateX(-100%)
</style>
