import { type Ref } from "vue";

/**
 * The keyboard half of a `role="menu"` dropdown: Escape leaves, the arrows move
 * focus over `role="menuitem"`. Escape is not preventDefaulted, so the page can
 * close what it has open; the arrows are, or the page scrolls.
 */
export interface MenuKeyboardOptions {
  /** Whether the menu is open. Closed menus pass every key straight through. */
  isOpen: () => boolean;
  /** Closes the menu. Returning focus is this composable's job, not its. */
  close: () => void;
  /** The control that opens the menu — Escape hands focus back to it. */
  trigger: Ref<HTMLElement | null>;
  /** The element holding the items; anything with role="menuitem" inside. */
  menu: Ref<HTMLElement | null>;
}

const ITEM_SELECTOR = '[role="menuitem"]';

export function useMenuKeyboard(options: MenuKeyboardOptions) {
  function items(): HTMLElement[] {
    const root = options.menu.value;
    if (!root) return [];
    return Array.from(root.querySelectorAll<HTMLElement>(ITEM_SELECTOR));
  }

  /** Wraps at both ends, the way a menu of a handful of items should. */
  function focusAt(list: HTMLElement[], index: number): void {
    list[((index % list.length) + list.length) % list.length]?.focus();
  }

  function handleMenuKeydown(event: KeyboardEvent): void {
    if (!options.isOpen()) return;

    if (event.key === "Escape") {
      options.close();
      options.trigger.value?.focus();
      return;
    }

    const list = items();
    if (!list.length) return;
    // -1 while focus is still on the trigger, which is where it starts.
    const current = list.indexOf(document.activeElement as HTMLElement);

    switch (event.key) {
      case "ArrowDown":
        event.preventDefault();
        focusAt(list, current + 1);
        break;
      case "ArrowUp":
        event.preventDefault();
        focusAt(list, current < 0 ? list.length - 1 : current - 1);
        break;
      case "Home":
        event.preventDefault();
        focusAt(list, 0);
        break;
      case "End":
        event.preventDefault();
        focusAt(list, list.length - 1);
        break;
    }
  }

  return { handleMenuKeydown };
}
