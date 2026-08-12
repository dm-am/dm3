/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref } from "vue";
import { useMenuKeyboard } from "./useMenuKeyboard";

/** A trigger button and a menu of three items, all in the document. */
function menuOf(count: number) {
  const trigger = document.createElement("button");
  const menu = document.createElement("ul");
  menu.setAttribute("role", "menu");
  for (let i = 0; i < count; i++) {
    const item = document.createElement("button");
    item.setAttribute("role", "menuitem");
    item.textContent = `item ${i}`;
    menu.appendChild(item);
  }
  document.body.append(trigger, menu);
  return {
    trigger,
    menu,
    items: Array.from(menu.querySelectorAll<HTMLElement>('[role="menuitem"]')),
  };
}

function press(key: string): KeyboardEvent {
  const event = new KeyboardEvent("keydown", { key, cancelable: true });
  return event;
}

describe("useMenuKeyboard", () => {
  beforeEach(() => {
    document.body.innerHTML = "";
  });

  it("closes on Escape and hands focus back to the trigger", () => {
    const { trigger, menu, items } = menuOf(2);
    const close = vi.fn();
    const { handleMenuKeydown } = useMenuKeyboard({
      isOpen: () => true,
      close,
      trigger: ref(trigger),
      menu: ref(menu),
    });
    items[1].focus();

    handleMenuKeydown(press("Escape"));

    expect(close).toHaveBeenCalledOnce();
    expect(document.activeElement).toBe(trigger);
  });

  it("leaves Escape to the rest of the page", () => {
    const { trigger, menu } = menuOf(2);
    const { handleMenuKeydown } = useMenuKeyboard({
      isOpen: () => true,
      close: vi.fn(),
      trigger: ref(trigger),
      menu: ref(menu),
    });

    const event = press("Escape");
    handleMenuKeydown(event);

    expect(event.defaultPrevented).toBe(false);
  });

  it("walks the items with the arrows, wrapping at both ends", () => {
    const { trigger, menu, items } = menuOf(3);
    const { handleMenuKeydown } = useMenuKeyboard({
      isOpen: () => true,
      close: vi.fn(),
      trigger: ref(trigger),
      menu: ref(menu),
    });
    trigger.focus();

    // Focus starts on the trigger, so the first press lands on the first item.
    handleMenuKeydown(press("ArrowDown"));
    expect(document.activeElement).toBe(items[0]);

    handleMenuKeydown(press("ArrowDown"));
    handleMenuKeydown(press("ArrowDown"));
    expect(document.activeElement).toBe(items[2]);

    handleMenuKeydown(press("ArrowDown"));
    expect(document.activeElement).toBe(items[0]);

    handleMenuKeydown(press("ArrowUp"));
    expect(document.activeElement).toBe(items[2]);
  });

  it("goes to the last item when the first press is ArrowUp", () => {
    const { trigger, menu, items } = menuOf(3);
    const { handleMenuKeydown } = useMenuKeyboard({
      isOpen: () => true,
      close: vi.fn(),
      trigger: ref(trigger),
      menu: ref(menu),
    });
    trigger.focus();

    handleMenuKeydown(press("ArrowUp"));

    expect(document.activeElement).toBe(items[2]);
  });

  it("jumps to the ends with Home and End", () => {
    const { trigger, menu, items } = menuOf(3);
    const { handleMenuKeydown } = useMenuKeyboard({
      isOpen: () => true,
      close: vi.fn(),
      trigger: ref(trigger),
      menu: ref(menu),
    });
    items[1].focus();

    handleMenuKeydown(press("End"));
    expect(document.activeElement).toBe(items[2]);

    handleMenuKeydown(press("Home"));
    expect(document.activeElement).toBe(items[0]);
  });

  it("swallows the arrows only while the menu is open", () => {
    const { trigger, menu } = menuOf(3);
    const close = vi.fn();
    const open = ref(false);
    const { handleMenuKeydown } = useMenuKeyboard({
      isOpen: () => open.value,
      close,
      trigger: ref(trigger),
      menu: ref(menu),
    });

    // A closed menu must not eat the arrow keys — the page still scrolls.
    const closedPress = press("ArrowDown");
    handleMenuKeydown(closedPress);
    expect(closedPress.defaultPrevented).toBe(false);
    expect(document.activeElement).toBe(document.body);

    open.value = true;
    const openPress = press("ArrowDown");
    handleMenuKeydown(openPress);
    expect(openPress.defaultPrevented).toBe(true);
  });

  it("does nothing when the menu element is not there yet", () => {
    const trigger = document.createElement("button");
    document.body.append(trigger);
    const { handleMenuKeydown } = useMenuKeyboard({
      isOpen: () => true,
      close: vi.fn(),
      trigger: ref(trigger),
      menu: ref<HTMLElement | null>(null),
    });

    const event = press("ArrowDown");
    expect(() => handleMenuKeydown(event)).not.toThrow();
    expect(event.defaultPrevented).toBe(false);
  });
});
