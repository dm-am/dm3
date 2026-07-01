import type { Directive, DirectiveBinding } from "vue";

interface ClickOutsideElement extends HTMLElement {
  _clickOutsideHandler?: (event: MouseEvent) => void;
}

/**
 * Directive to detect clicks outside an element
 * Usage: v-click-outside="handleClose"
 */
export const vClickOutside: Directive<ClickOutsideElement, () => void> = {
  mounted(el: ClickOutsideElement, binding: DirectiveBinding<() => void>) {
    el._clickOutsideHandler = (event: MouseEvent) => {
      // Listen in capture phase so this runs BEFORE any @click inside the
      // element. If we listened in bubble phase, a handler inside could
      // mutate the DOM (e.g. v-if remove the clicked node) before this
      // runs, making `el.contains(target)` return false and firing a
      // false-positive outside-click. In capture phase the DOM is still
      // intact, so contains() is always accurate.
      const target = event.target as Node | null;
      if (
        target &&
        !el.contains(target) &&
        typeof binding.value === "function"
      ) {
        binding.value();
      }
    };
    document.addEventListener("click", el._clickOutsideHandler, true);
  },

  unmounted(el: ClickOutsideElement) {
    if (el._clickOutsideHandler) {
      document.removeEventListener("click", el._clickOutsideHandler, true);
      delete el._clickOutsideHandler;
    }
  },
};
