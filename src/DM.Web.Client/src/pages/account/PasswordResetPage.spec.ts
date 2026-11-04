/**
 * @vitest-environment jsdom
 */

/**
 * The page the password-recovery letter points at: the field and the line
 * under it (the caret, the description, a refusal dropped at the first edit)
 * and the two dead ends. The checking line is tokenPages.spec.ts's.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import PasswordResetPage from "./PasswordResetPage.vue";
import { push } from "./guestPages.doubles";

vi.mock("vue-router", async () =>
  (await import("./guestPages.doubles")).vueRouter(),
);

/** Attached to the document, for the caret. */
const options = () => ({
  global: {
    plugins: [createPinia()],
    stubs: { RouterLink: { template: "<a><slot /></a>" } },
  },
  attachTo: document.body,
});

const BREACHED = "Пароль встречается в утечках";

enableAutoUnmount(afterEach);

beforeEach(() => {
  setActivePinia(createPinia());
  vi.restoreAllMocks();
  // Not a spy, so restoreAllMocks does not reach it.
  push.mockClear();
  document.body.innerHTML = "";
  // The value arrives in the fragment.
  window.location.hash = "#token=a-token";
});

/** Land on the card with the form on it. */
const atForm = async () => {
  vi.spyOn(accountApi, "getPasswordResetTokenInfo").mockResolvedValue({
    data: { status: "ready" },
    error: null,
  } as never);

  const wrapper = mount(PasswordResetPage, options());
  await flushPromises();
  return wrapper;
};

/** Land on the card with the answer the check came back with. */
const atAnswer = (answer: unknown) => async () => {
  vi.spyOn(accountApi, "getPasswordResetTokenInfo").mockResolvedValue(
    answer as never,
  );

  const wrapper = mount(PasswordResetPage, options());
  await flushPromises();
  return wrapper;
};

const atExpiredLink = atAnswer({ data: { status: "expired" }, error: null });
const atDeadLink = atAnswer({ data: null, error: { status: 404 } });

/** Hand in a password the server refuses. */
const handInRefusedPassword = async (wrapper: VueWrapper) => {
  vi.spyOn(accountApi, "completePasswordReset").mockResolvedValue({
    data: null,
    error: { status: 400, errors: { newPassword: [BREACHED] } },
  } as never);

  await wrapper.get("#reset-new-password").setValue("correct horse");
  await wrapper.get("form").trigger("submit");
  await flushPromises();
};

/** What the strip under the field says, and whether the bar is red. */
const line = (wrapper: VueWrapper) => wrapper.get(".auth-meter").text();
const barIsRed = (wrapper: VueWrapper) =>
  wrapper.get(".strength-fill").classes().includes("error");

describe("the page of the new password", () => {
  it("puts the caret in the field of the new password", async () => {
    const wrapper = await atForm();

    expect(document.activeElement).toBe(
      wrapper.get("#reset-new-password").element,
    );
  });

  /** The heading says which password, so the label is the bare noun. */
  it("does not repeat the heading of the card in the label", async () => {
    const wrapper = await atForm();

    expect(wrapper.get(".auth-title").text()).toBe("Новый пароль");
    expect(wrapper.get("label[for='reset-new-password']").text()).toBe(
      "Пароль",
    );
  });

  /** An edit drops the refusal: the server has not seen what the field holds now. */
  it("drops the refusal of the server at the first edit of the password", async () => {
    const wrapper = await atForm();
    await handInRefusedPassword(wrapper);

    expect(line(wrapper)).toContain(BREACHED);
    expect(barIsRed(wrapper)).toBe(true);

    await wrapper.get("#reset-new-password").setValue("correct horse battery");

    expect(line(wrapper)).not.toContain(BREACHED);
    expect(line(wrapper)).toBe("Надежный");
    expect(barIsRed(wrapper)).toBe(false);
  });

  /** The line under the field is its description, and a refusal marks the field invalid. */
  it("ties the line under the field to the field, invalid while a refusal stands", async () => {
    const wrapper = await atForm();
    const field = wrapper.get("#reset-new-password");

    expect(field.attributes("aria-describedby")).toBe("reset-password-message");
    expect(wrapper.get(".auth-meter").attributes("id")).toBe(
      "reset-password-message",
    );
    expect(field.attributes("aria-invalid")).toBeUndefined();

    await handInRefusedPassword(wrapper);
    expect(field.attributes("aria-invalid")).toBe("true");

    await field.setValue("correct horse battery");
    expect(field.attributes("aria-invalid")).toBeUndefined();
  });

  /** A refused password is not sent twice: the button opens at the first edit. */
  it("shuts the button while the refusal of the server is on screen", async () => {
    const wrapper = await atForm();
    const button = () => wrapper.get("button[type='submit']");

    await wrapper.get("#reset-new-password").setValue("correct horse");
    expect(button().attributes("disabled")).toBeUndefined();

    await handInRefusedPassword(wrapper);

    expect(line(wrapper)).toContain(BREACHED);
    expect(button().attributes("disabled")).toBe("");

    await wrapper.get("#reset-new-password").setValue("correct horse battery");

    expect(button().attributes("disabled")).toBeUndefined();
  });

  /** The finished card says the password is updated. */
  it("says under the heading that the password is updated", async () => {
    const wrapper = await atForm();
    vi.spyOn(accountApi, "completePasswordReset").mockResolvedValue({
      data: {},
      error: null,
    } as never);

    await wrapper.get("#reset-new-password").setValue("correct horse battery");
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(wrapper.get(".auth-title").text()).toBe("Новый пароль сохранен");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Ваш пароль был успешно обновлен",
    );
  });
});

/** The two dead ends, each with a way out that is a control. */
describe("the dead ends of the reset page", () => {
  it("gives the expired link a button that names the errand", async () => {
    const wrapper = await atExpiredLink();

    expect(wrapper.get(".auth-title").text()).toBe("Ссылка устарела");
    // The heading says the link ran out, so the line names what ran out.
    expect(wrapper.get(".auth-text").text()).toBe(
      "Срок действия сброса пароля истек",
    );

    // The surface's single full-width action, as on the expired activation link.
    const ask = wrapper.get("button.auth-button");
    expect(ask.text()).toBe("Запросить новую ссылку");
    expect(wrapper.findAll(".auth-note a")).toHaveLength(0);

    await ask.trigger("click");
    expect(push).toHaveBeenLastCalledWith({
      path: "/",
      query: { action: "recovery" },
    });
  });

  /** One way out of a dead link, as on every screen that answers one. */
  it("gives the dead link one way out, the home page", async () => {
    const wrapper = await atDeadLink();

    expect(wrapper.get(".auth-title").text()).toBe("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );

    const ways = wrapper.findAll("button");
    expect(ways.map((button) => button.text())).toEqual(["На главную"]);
    // The single action of the card, as wide as it.
    expect(ways[0].classes()).toContain("auth-button");
    expect(wrapper.find(".form-actions").exists()).toBe(false);
    // Nothing is left underlined in the sentence above.
    expect(wrapper.findAll(".auth-note")).toHaveLength(0);

    await ways[0].trigger("click");
    expect(push).toHaveBeenLastCalledWith("/");
  });
});
