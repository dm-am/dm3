/**
 * @vitest-environment jsdom
 */

/**
 * "Удаление аккаунта", the form window the removal of an account is made in.
 *
 * The window is the site's own (Dialog, Form, FormField): titled by what it
 * does, the main action named by its result, "Отмена" beside it. The server
 * requires a reason, and an account removed with nothing written down leaves
 * the next moderator with a missing person and no record of who decided what,
 * so the main button is shut until there is one and an empty reason never
 * reaches the wire. The window runs the request, prints a refusal in itself
 * and reports success to its opener, who leaves the profile.
 */
import { afterEach, beforeEach, describe, it, expect, vi } from "vitest";
import { defineComponent, h } from "vue";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { setScrollContainer } from "@/shared/lib/scroll";
import AccountRemovalDialog from "./AccountRemovalDialog.vue";

const removeAccount = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  moderationApi: { removeAccount },
}));

// The library tier is a box that holds the card: every closing the window
// makes is its own decision, and that decision is what is read below.
vi.mock("vue-final-modal", () => ({
  VueFinalModal: defineComponent({
    name: "VueFinalModal",
    setup:
      (_props, { slots }) =>
      () =>
        h("div", { class: "vfm-content" }, slots.default?.()),
  }),
}));

function render() {
  return mount(AccountRemovalDialog, {
    attachTo: document.body,
    props: { targetUsername: "reader" },
  });
}

type Wrapper = ReturnType<typeof render>;

const main = (w: Wrapper) => w.get(".controls button.primary");

enableAutoUnmount(afterEach);

afterEach(() => {
  document.body.innerHTML = "";
  setScrollContainer(null);
});

describe("the account removal window", () => {
  beforeEach(() => removeAccount.mockReset());

  it("is titled by what it does, names the account and its two actions", () => {
    const w = render();

    expect(w.get("h2").text()).toBe("Удаление аккаунта");
    expect(w.text()).toContain("Аккаунт: reader");
    expect(w.findAll(".controls button").map((b) => b.text())).toEqual([
      "Удалить аккаунт",
      "Отмена",
    ]);
    expect(w.get(".form-field-label label").text()).toBe("Причина");
  });

  it("keeps the main button shut until a reason is written", async () => {
    const w = render();
    expect(main(w).attributes("disabled")).toBeDefined();

    await w.get("textarea").setValue("   ");
    expect(main(w).attributes("disabled")).toBeDefined();

    await w.get("textarea").setValue("Обращение 1234");
    expect(main(w).attributes("disabled")).toBeUndefined();
  });

  it("sends the trimmed reason with the name, and reports the removal", async () => {
    removeAccount.mockResolvedValue({ data: undefined, error: null });
    const w = render();

    await w.get("textarea").setValue("  Обращение 1234  ");
    await main(w).trigger("click");
    await flushPromises();

    // The window prints a refusal in itself, so the interceptor keeps quiet.
    expect(removeAccount).toHaveBeenCalledWith("reader", "Обращение 1234", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(w.emitted("success")).toHaveLength(1);
  });

  /**
   * A refusal leaves the moderator where he is, with the reason he typed still
   * in the box, and says why in the window.
   */
  it("keeps the reason and says a refusal in the window", async () => {
    removeAccount.mockResolvedValue({
      data: undefined,
      error: { status: 403, title: "Недостаточно прав" },
    });
    const w = render();

    await w.get("textarea").setValue("Обращение 1234");
    await main(w).trigger("click");
    await flushPromises();

    expect(w.emitted("success")).toBeUndefined();
    expect((w.get("textarea").element as HTMLTextAreaElement).value).toBe(
      "Обращение 1234",
    );
    expect(w.get(".form-refusal").text()).toBe("Недостаточно прав");
  });
});
