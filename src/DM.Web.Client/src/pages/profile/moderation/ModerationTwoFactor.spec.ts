/**
 * @vitest-environment jsdom
 */

/**
 * Taking a colleague's second factor off: the confirmation names the
 * consequence, and afterwards the line in place of the button says what the
 * press did, with nothing said out loud.
 */
import { describe, expect, it, vi, afterEach, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { accountApi } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import ModerationTwoFactor from "./ModerationTwoFactor.vue";

/** Every wrapper mounted by a test, so the teleported dialogs go away with it. */
const mounted: { unmount: () => void }[] = [];

function mountBlock() {
  const wrapper = mount(ModerationTwoFactor, {
    props: { targetUsername: "colleague" },
    attachTo: document.body,
  });
  mounted.push(wrapper);
  return wrapper;
}

/** The teleported confirmation; tests unmount theirs, or the next one finds it. */
const dialog = () =>
  [...document.querySelectorAll(".dialog-container")].at(-1) ?? null;

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

/** Presses the confirmation of the dialog this block has just opened. */
const confirmDialog = async () => {
  dialog()?.querySelector<HTMLButtonElement>(".dialog-btn-submit")?.click();
  await flushPromises();
};

describe("taking a colleague's factor off", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount());
  });

  // The button names the action in the full term of the glossary.
  it("is one button", () => {
    const wrapper = mountBlock();

    expect(wrapper.text()).toBe("Отключить двухфакторную аутентификацию");
    expect(wrapper.findAll("button")).toHaveLength(1);
  });

  it("names the consequence before it is done", async () => {
    const wrapper = mountBlock();

    await wrapper.find(".mod-two-factor_actions button").trigger("click");

    // The full term in the heading, and the one consequence the reader weighs under it.
    expect(dialog()?.textContent).toContain(
      "Отключить двухфакторную аутентификацию?",
    );
    expect(dialog()?.textContent).toContain("Сессии этого аккаунта завершатся");
    expect(dialog()?.textContent).toContain(
      "входить он будет по одному паролю",
    );
  });

  it("reports the change once the server confirms it", async () => {
    const clear = vi
      .spyOn(accountApi, "clearTwoFactorFor")
      .mockResolvedValue({ data: null, error: null } as never);

    const wrapper = mountBlock();
    await wrapper.find(".mod-two-factor_actions button").trigger("click");
    await confirmDialog();

    // The window prints a refusal in itself, so the interceptor keeps quiet.
    expect(clear).toHaveBeenCalledWith("colleague", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.emitted("updated")).toHaveLength(1);
    expect(dialog()).toBeNull();
  });

  /** The block stops offering the action and says both halves of what it did, in the confirmation's words. */
  it("says the factor is off and the sessions over, where the button was", async () => {
    vi.spyOn(accountApi, "clearTwoFactorFor").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    const wrapper = mountBlock();
    await wrapper.find(".mod-two-factor_actions button").trigger("click");
    await confirmDialog();

    const line = wrapper.find(".mod-two-factor_state");
    expect(line.text()).toBe(
      "Двухфакторная аутентификация отключена, сессии завершены",
    );
    // Whole on every width.
    expect(line.attributes("data-fit-shrink")).toBeUndefined();
    // A live region, so a screen reader gets the answer too.
    expect(line.attributes("role")).toBe("status");
    expect(wrapper.find(".mod-two-factor_actions").exists()).toBe(false);
  });

  it("says nothing happened when the server refused", async () => {
    vi.spyOn(accountApi, "clearTwoFactorFor").mockResolvedValue({
      data: null,
      error: { status: 409, title: "Двухфакторная аутентификация не включена" },
    } as never);

    const wrapper = mountBlock();
    await wrapper.find(".mod-two-factor_actions button").trigger("click");
    await confirmDialog();

    expect(wrapper.emitted("updated")).toBeUndefined();
    // Nothing was taken off: the offer stays.
    expect(wrapper.find(".mod-two-factor_state").exists()).toBe(false);
    expect(wrapper.find(".mod-two-factor_actions button").exists()).toBe(true);
    // Said in the window that asked, which stays open, and nowhere else.
    expect(
      dialog()?.querySelector(".dialog-refusal")?.textContent?.trim(),
    ).toBe("Двухфакторная аутентификация не включена");
    expect(saidOutLoud()).toEqual([]);
  });
});
