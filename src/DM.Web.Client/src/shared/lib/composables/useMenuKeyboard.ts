import { type Ref } from "vue";

/**
 * The keyboard half of a `role="menu"` dropdown.
 *
 * Three menus on the site announce themselves as menus — the sign-out menu in
 * the header, the sort dropdown and the "и еще N" bubble in the filters — and
 * a reader who hears "menu" expects Escape to leave it and the arrows to walk
 * its items. None of them did that: the trigger keeps focus when the menu
 * opens, so pressing it again was the only key that closed anything, and
 * nothing at all moved between the items.
 *
 * Focus is what moves here, not a highlight: a highlighted index is an
 * affordance the template has to paint, and none of these menus has one.
 * Items are found by their `role="menuitem"`, so the markup that makes the
 * promise to the reader is the same markup this reads.
 *
 * Escape is not `preventDefault`ed: it is how the rest of the page closes what
 * it has open too, and swallowing it inside a menu would strand anything
 * around it. The arrows are, or the page scrolls out from under the menu.
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
