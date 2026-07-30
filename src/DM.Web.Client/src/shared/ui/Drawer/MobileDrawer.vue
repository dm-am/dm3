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
import { ref, watch, nextTick, onBeforeUnmount } from "vue";
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
let previouslyFocused: HTMLElement | null = null;
let lockedScrollEl: HTMLElement | null = null;
let lockedScrollPrevOverflow = "";

function close() {
  emit("update:modelValue", false);
}

function lockScroll() {
  // The app's real scroll container is `.main` (App.vue) — `body` is
  // permanently `overflow: hidden` (Reset.sass), so locking body has no
  // effect here.
  lockedScrollEl = document.querySelector<HTMLElement>(".main");
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

watch(
  () => props.modelValue,
  (open) => {
    if (open) {
      previouslyFocused = document.activeElement as HTMLElement | null;
      lockScroll();
      nextTick(() => closeBtn.value?.focus());
    } else {
      unlockScroll();
      previouslyFocused?.focus?.();
      previouslyFocused = null;
    }
  },
  // Also handles the (unusual but possible) case of the drawer being
  // mounted already open — locks scroll immediately instead of only on
  // the next open/close transition.
  { immediate: true },
);

onBeforeUnmount(() => {
  unlockScroll();
});

// Focusable elements inside the drawer, in DOM order.
function focusables(): HTMLElement[] {
  if (!panel.value) return [];
  return Array.from(
    panel.value.querySelectorAll<HTMLElement>(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
    ),
  ).filter((el) => !el.hasAttribute("disabled"));
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === "Escape") {
    e.preventDefault();
    close();
    return;
  }
  if (e.key === "Tab") {
    // Trap focus within the drawer while open.
    const items = focusables();
    if (!items.length) return;
    const first = items[0];
    const last = items[items.length - 1];
    const active = document.activeElement as HTMLElement | null;
    if (e.shiftKey && active === first) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && active === last) {
      e.preventDefault();
      first.focus();
    }
  }
}

function handleScrimClick(e: MouseEvent) {
  if (e.target === e.currentTarget) {
    close();
  }
}
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
        @click="handleScrimClick"
        @keydown="handleKeydown"
      >
        <div
          ref="panel"
          class="drawer-panel"
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
@import "@/assets/styles/ZIndex"

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
