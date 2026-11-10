/**
 * @vitest-environment jsdom
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import type { EmailAvailability } from "@/shared/api/models/account";
import { accountApi } from "@/entities/user";
import {
  askAbout,
  control,
  fieldProblem,
  heldCheck,
  mountForm,
  resetRegistrationForm,
  submitRegistration,
  toPasswordStep,
} from "./RegistrationForm.fixtures";
import RegistrationForm from "./RegistrationForm.vue";
import { register } from "./authForms.doubles";
import { problem } from "./authForms.fixtures";

vi.mock("@/entities/user", async () =>
  (await import("./authForms.doubles")).userEntity(),
);
vi.mock("@/shared/lib/composables/useToast", async () =>
  (await import("./authForms.doubles")).toastComposable(),
);

describe("RegistrationForm", () => {
  beforeEach(resetRegistrationForm);

  it("offers the way in when the address is already somebody's", async () => {
    vi.mocked(accountApi.checkEmail).mockResolvedValue({
      data: { isAvailable: false, reason: "Taken" },
      error: null,
    });

    const wrapper = await askAbout();

    expect(wrapper.text()).toContain("Аккаунт с этой почтой уже существует");

    const login = control(wrapper, "Войти");
    expect(login).toBeDefined();
    await login?.trigger("click");

    expect(
      wrapper.findComponent(RegistrationForm).emitted("openLogin")?.[0],
    ).toEqual(["reader@dm.am"]);
  });

  // The check on blur can be skipped by Enter, so the submit has to offer the way in too.
  it("offers the way in when the submit is what learns the address is taken", async () => {
    vi.mocked(register).mockResolvedValue({
      ...problem(400, ""),
      errors: { email: ["Taken"] },
    } as never);

    const wrapper = await submitRegistration();

    expect(wrapper.text()).toContain("Аккаунт с этой почтой уже существует");

    const login = control(wrapper, "Войти");
    expect(login).toBeDefined();
    await login?.trigger("click");
    expect(
      wrapper.findComponent(RegistrationForm).emitted("openLogin")?.[0],
    ).toEqual(["reader@dm.am"]);
  });

  // A removed account's address answers as taken: the server sends "Taken" for both.
  it.each([["Taken", "the reason the wire carries"]])(
    "answers %s with the way in and no third verdict, %s",
    async (reason) => {
      vi.mocked(accountApi.checkEmail).mockResolvedValue({
        data: { isAvailable: false, reason } as EmailAvailability,
        error: null,
      });

      const wrapper = await askAbout();

      expect(wrapper.text()).toContain("Аккаунт с этой почтой уже существует");
      expect(wrapper.text()).not.toContain("удален");
      expect(control(wrapper, "Восстановить?")).toBeUndefined();
      // One link in the label row, and it is the way in.
      expect(
        wrapper
          .findAll(".auth-label-row button")
          .map((button) => button.text()),
      ).toEqual(["Войти"]);
    },
  );

  it.each([
    ["Taken", "Войти"],
    ["PendingActivation", "Отправить письмо повторно"],
  ])(
    "takes down the link of a %s address on the first character typed",
    async (reason, caption) => {
      vi.mocked(accountApi.checkEmail).mockResolvedValue({
        data: { isAvailable: false, reason } as EmailAvailability,
        error: null,
      });

      const wrapper = await askAbout("lexx@dm.am");
      const email = wrapper.find("#registration-email");

      expect(control(wrapper, caption)).toBeDefined();

      await email.setValue("lexx2@dm.am");

      expect(control(wrapper, caption)).toBeUndefined();
      expect(wrapper.find("#registration-email-message").text()).toBe("");
    },
  );

  it("names what stands in the way when the server refuses the address", async () => {
    vi.mocked(register).mockResolvedValue(fieldProblem("email", "Taken"));

    const wrapper = await submitRegistration();

    expect(
      wrapper.findComponent(RegistrationForm).emitted("success"),
    ).toBeUndefined();
    expect(wrapper.find("#registration-email").exists()).toBe(true);
    expect(wrapper.find("#registration-email-message").text()).toBe(
      "Аккаунт с этой почтой уже существует",
    );
    expect(wrapper.text()).not.toContain("Уже занято");
  });

  it("shows the address it is about, and lets the reader back to it", async () => {
    const wrapper = await mountForm();
    await toPasswordStep(wrapper);

    expect(wrapper.find(".auth-value").text()).toBe("reader@dm.am");
    expect(wrapper.find("#registration-email").exists()).toBe(false);

    await control(wrapper, "Изменить")?.trigger("click");

    expect(
      (wrapper.find("#registration-email").element as HTMLInputElement).value,
    ).toBe("reader@dm.am");
  });

  it("checks the address once when Enter arrives twice", async () => {
    const wrapper = await mountForm();
    const email = wrapper.find("#registration-email");
    await email.setValue("reader@dm.am");

    // A held Enter repeats the keydown while the first check is on the wire.
    void email.trigger("keydown.enter");
    void email.trigger("keydown.enter");
    await flushPromises();

    expect(accountApi.checkEmail).toHaveBeenCalledTimes(1);
  });

  // The click blurs the field and starts the check, so the press lands while it is on the wire.
  it("takes the first press while the check is still on the wire", async () => {
    const answer = heldCheck();

    const wrapper = await mountForm();
    const email = wrapper.find("#registration-email");
    await email.setValue("reader@dm.am");

    await email.trigger("blur");
    expect(
      wrapper.find("button.auth-button").attributes("disabled"),
    ).toBeUndefined();

    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(wrapper.find("button.auth-button").attributes("aria-busy")).toBe(
      "true",
    );
    expect(wrapper.find("#registration-email").exists()).toBe(true);

    answer({ data: { isAvailable: true }, error: null });
    await flushPromises();

    expect(accountApi.checkEmail).toHaveBeenCalledTimes(1);
    expect(wrapper.find("#registration-password").exists()).toBe(true);
  });

  it("lights the red only when the field is done with, and drops it on the next keystroke", async () => {
    vi.mocked(accountApi.checkEmail).mockResolvedValue({
      data: { isAvailable: false, reason: "Taken" },
      error: null,
    });

    const wrapper = await mountForm();
    const email = wrapper.find("#registration-email");

    await email.setValue("reader@");
    expect(wrapper.find("#registration-email-message").classes()).not.toContain(
      "auth-message--error",
    );

    await email.setValue("reader@dm.am");
    await email.trigger("blur");
    await flushPromises();

    const strip = wrapper.find("#registration-email-message");
    expect(strip.text()).toBe("Аккаунт с этой почтой уже существует");
    expect(strip.classes()).toContain("auth-message--error");

    await email.setValue("reader2@dm.am");
    expect(wrapper.find("#registration-email-message").text()).toBe("");
    expect(wrapper.find("#registration-email-message").classes()).not.toContain(
      "auth-message--error",
    );
  });

  it("does not light the red on an answer the reader has already typed past", async () => {
    // The answer about the address he had lands after he kept typing.
    const answer = heldCheck();

    const wrapper = await mountForm();
    const email = wrapper.find("#registration-email");
    await email.setValue("reader@dm.am");
    await email.trigger("blur");

    await email.setValue("reader2@dm.am");

    answer({ data: { isAvailable: false, reason: "Taken" }, error: null });
    await flushPromises();

    const strip = wrapper.find("#registration-email-message");
    expect(strip.text()).toBe("");
    expect(strip.classes()).not.toContain("auth-message--error");
    expect(control(wrapper, "Войти")).toBeUndefined();
  });
});
