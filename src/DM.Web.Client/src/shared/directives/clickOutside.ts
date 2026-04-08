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
      const target = event.target as Node;
      if (!el.contains(target) && typeof binding.value === "function") {
        binding.value();
      }
    };
    document.addEventListener("click", el._clickOutsideHandler);
  },

  unmounted(el: ClickOutsideElement) {
    if (el._clickOutsideHandler) {
      document.removeEventListener("click", el._clickOutsideHandler);
      delete el._clickOutsideHandler;
    }
  },
};
