/**
 * @vitest-environment jsdom
 */

/** The self-rolled windows close with the session through their dialog shell. */
import { afterEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, ref } from "vue";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import { useDialogShell } from "./useDialogShell";
import { closeOpenWindows, openWindowCount } from "./useOpenWindows";

enableAutoUnmount(afterEach);

/** A window of the shell: shown from the start, and what Escape would do. */
function shellWith() {
  const dismiss = vi.fn();
  const shown = ref(true);
  const Host = defineComponent({
    setup() {
      useDialogShell({
        show: shown,
        container: ref(null),
        initialFocus: () => null,
        onDismiss: dismiss,
      });
      return () => h("div");
    },
  });
  return { wrapper: mount(Host), dismiss, shown };
}

describe("a window of the dialog shell at the end of the session", () => {
  it("is closed the way Escape closes it", () => {
    const { dismiss } = shellWith();

    closeOpenWindows();

    expect(dismiss).toHaveBeenCalledTimes(1);
  });

  it("is not asked once it has closed", () => {
    const { dismiss, shown } = shellWith();

    shown.value = false;
    closeOpenWindows();

    expect(dismiss).not.toHaveBeenCalled();
  });

  it("leaves the list with its screen", () => {
    const before = openWindowCount();
    const { wrapper } = shellWith();
    expect(openWindowCount()).toBe(before + 1);

    wrapper.unmount();

    expect(openWindowCount()).toBe(before);
  });
});
