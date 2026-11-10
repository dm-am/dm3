/**
 * @vitest-environment jsdom
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { nextTick } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { accountApi } from "@/entities/user";
import RegistrationForm from "./RegistrationForm.vue";
import { register, toast } from "./authForms.doubles";
import { address, expectWaitRunsDown, problem } from "./authForms.fixtures";
import {
  acceptRules,
  clock,
  control,
  fieldProblem,
  mountForm,
  resetRegistrationForm,
  submitRegistration,
  toPasswordStep,
} from "./RegistrationForm.fixtures";

vi.mock("@/entities/user", async () =>
  (await import("./authForms.doubles")).userEntity(),
);
vi.mock("@/shared/lib/composables/useToast", async () =>
  (await import("./authForms.doubles")).toastComposable(),
);

describe("RegistrationForm", () => {
  beforeEach(resetRegistrationForm);

  it("does not promise a letter the server never took", async () => {
    vi.mocked(register).mockResolvedValue(problem(500, "Ошибка сервера"));

    const wrapper = await submitRegistration();

    expect(
      wrapper.findComponent(RegistrationForm).emitted("success"),
    ).toBeUndefined();
  });

  describe("a rate limit", () => {
    beforeEach(() => {
      // Only the interval: Date.now is already controlled by the fixtures.
      vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it("stands under the address when the check was refused", async () => {
      vi.mocked(accountApi.checkEmail).mockResolvedValue({
        data: null,
        error: problem(429, "Слишком много запросов", 3),
      });

      const wrapper = await mountForm();
      await wrapper.find("#registration-email").setValue("reader@dm.am");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      // The limit refused the check, so nothing is known about the address yet.
      expect(wrapper.find("#registration-email").exists()).toBe(true);
      expect(toast.warning).not.toHaveBeenCalled();
      await expectWaitRunsDown(wrapper, "#registration-email-message");
    });

    it("takes the line of the scale when the registration was refused", async () => {
      vi.mocked(register).mockResolvedValue(
        problem(429, "Слишком много запросов", 3),
      );

      const wrapper = await submitRegistration();

      expect(
        wrapper.findComponent(RegistrationForm).emitted("success"),
      ).toBeUndefined();
      const refusal = wrapper.find(".password-strength .warning-text");
      expect(refusal.text()).toBe(
        "Слишком много попыток, попробуйте через 3 секунды",
      );
      expect(wrapper.find(".strength-fill").classes()).toContain("error");
      expect(wrapper.find("button.auth-button").attributes("disabled")).toBe(
        "",
      );
      expect(toast.warning).not.toHaveBeenCalled();

      vi.advanceTimersByTime(3000);
      await nextTick();
      expect(wrapper.find(".password-strength .warning-text").exists()).toBe(
        false,
      );
      expect(wrapper.find(".password-strength .strength-text").exists()).toBe(
        true,
      );
      expect(
        wrapper.find("button.auth-button").attributes("disabled"),
      ).toBeUndefined();
    });

    // The modal keeps the component mounted, so a running interval would outlive it.
    it("drops the wait when the dialog closes", async () => {
      vi.mocked(register).mockResolvedValue(
        problem(429, "Слишком много запросов", 30),
      );

      const wrapper = await submitRegistration();
      expect(vi.getTimerCount()).toBe(1);

      wrapper.findComponent({ name: "AuthDialog" }).vm.$emit("beforeClose");
      await nextTick();

      expect(vi.getTimerCount()).toBe(0);
    });

    it("drops the wait when the card leaves the page", async () => {
      vi.mocked(register).mockResolvedValue(
        problem(429, "Слишком много запросов", 30),
      );

      const wrapper = await submitRegistration();
      expect(vi.getTimerCount()).toBe(1);

      wrapper.unmount();

      expect(vi.getTimerCount()).toBe(0);
    });
  });

  it("says what the server named when it refused the address", async () => {
    vi.mocked(register).mockResolvedValue(
      problem(409, "Почта уже зарегистрирована"),
    );

    const wrapper = await submitRegistration();

    expect(
      wrapper.findComponent(RegistrationForm).emitted("success"),
    ).toBeUndefined();
    expect(wrapper.text()).toContain("Почта уже зарегистрирована");
  });

  it("hands the address and the lifetime the answer named on once the registration is pending", async () => {
    vi.mocked(register).mockResolvedValue(null);

    const wrapper = await submitRegistration();

    expect(
      wrapper.findComponent(RegistrationForm).emitted("success")?.[0],
    ).toEqual(["reader@dm.am", 30]);
  });

  it("states the length rule before it can be broken", async () => {
    const wrapper = await mountForm();
    await toPasswordStep(wrapper);

    expect(wrapper.find(".password-strength").exists()).toBe(true);
    expect(wrapper.text()).toContain("Минимум 8 символов");
  });

  it("keeps the rules box shut until the rules were opened", async () => {
    const wrapper = await mountForm();
    await toPasswordStep(wrapper);

    const box = wrapper.find("#registration-rules");
    expect(box.attributes("disabled")).toBeDefined();

    const tooltip = wrapper.findComponent(Tooltip);
    expect(tooltip.props("text")).toBe("Сначала откройте правила сайта");
    expect(tooltip.props("disabled")).toBe(false);

    await wrapper.find('a[href="/rules"]').trigger("click");

    expect(wrapper.find("#registration-rules").attributes("disabled")).toBe(
      undefined,
    );
    expect(wrapper.findComponent(Tooltip).props("disabled")).toBe(true);
  });

  it("does not register anybody who has not accepted the rules", async () => {
    vi.mocked(register).mockResolvedValue(null);

    const wrapper = await mountForm();
    await toPasswordStep(wrapper);
    await wrapper.find("#registration-password").setValue("correct horse");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(register).not.toHaveBeenCalled();

    await acceptRules(wrapper);
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(register).toHaveBeenCalledTimes(1);
  });

  it("puts the server's word about the password in the line of the scale", async () => {
    vi.mocked(register).mockResolvedValue(
      fieldProblem("password", "Пароль найден в утечке, выберите другой"),
    );

    const wrapper = await submitRegistration();

    expect(wrapper.text()).toContain("Пароль найден в утечке, выберите другой");
    expect(wrapper.find(".strength-fill").classes()).toContain("error");

    await wrapper.find("form").trigger("submit");
    await flushPromises();
    expect(register).toHaveBeenCalledTimes(1);

    await wrapper.find("#registration-password").setValue("another passphrase");
    expect(wrapper.text()).not.toContain("Пароль найден в утечке");
    await wrapper.find("form").trigger("submit");
    await flushPromises();
    expect(register).toHaveBeenCalledTimes(2);
  });

  it("takes the agreement and the policy by the act, not by a second box", async () => {
    const wrapper = await mountForm();
    await toPasswordStep(wrapper);

    expect(wrapper.findAll('input[type="checkbox"]')).toHaveLength(1);
    expect(wrapper.find(".auth-footnote").text()).toBe(
      "Регистрируясь, вы принимаете пользовательское соглашение и политику конфиденциальности",
    );
  });

  it("registers once when the submit is repeated", async () => {
    vi.mocked(register).mockResolvedValue(null);

    const wrapper = await mountForm();
    await toPasswordStep(wrapper);
    await wrapper.find("#registration-password").setValue("correct horse");
    await acceptRules(wrapper);

    // A held Enter submits again while the first registration is in flight.
    const form = wrapper.find("form");
    void form.trigger("submit");
    void form.trigger("submit");
    await flushPromises();

    expect(register).toHaveBeenCalledTimes(1);
  });

  it("points each field at the line standing under it", async () => {
    vi.mocked(accountApi.checkEmail).mockResolvedValue({
      data: { isAvailable: false, reason: "Taken" },
      error: null,
    });

    const wrapper = await mountForm();
    const email = wrapper.find("#registration-email");
    expect(email.attributes("aria-describedby")).toBe(
      "registration-email-message",
    );
    expect(wrapper.find("#registration-email-message").exists()).toBe(true);
    expect(email.attributes("aria-invalid")).toBeUndefined();

    await email.setValue("reader@dm.am");
    await email.trigger("blur");
    await flushPromises();

    expect(wrapper.find("#registration-email").attributes("aria-invalid")).toBe(
      "true",
    );
  });

  it("points the password at the scale, and marks the refusal on the field", async () => {
    vi.mocked(register).mockResolvedValue(
      fieldProblem("password", "Пароль найден в утечке, выберите другой"),
    );

    const wrapper = await mountForm();
    await toPasswordStep(wrapper);

    const password = wrapper.find("#registration-password");
    expect(password.attributes("aria-describedby")).toBe(
      "registration-password-message",
    );
    expect(wrapper.find("#registration-password-message").text()).toContain(
      "Минимум 8 символов",
    );
    expect(password.attributes("aria-invalid")).toBeUndefined();

    await password.setValue("correct horse battery");
    await acceptRules(wrapper);
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(wrapper.find("#registration-password-message").text()).toContain(
      "Пароль найден в утечке, выберите другой",
    );
    expect(
      wrapper.find("#registration-password").attributes("aria-invalid"),
    ).toBe("true");
  });

  it("puts the caret in the first field the moment the card opens", async () => {
    // Focus is only real for an element in the document.
    const wrapper = await mountForm(document.body);
    await flushPromises();

    expect(document.activeElement).toBe(
      wrapper.find("#registration-email").element,
    );

    wrapper.unmount();
  });

  it("closes the dialog and opens support on the recovery category", async () => {
    const wrapper = await mountForm();

    await control(wrapper, "поддержку")?.trigger("click");
    await flushPromises();

    expect(
      wrapper.findComponent(RegistrationForm).emitted("cancel"),
    ).toHaveLength(1);
    expect(address()).toBe("/support?subtype=AccessRecovery");
  });

  it("sits out the minimum fill time instead of refusing the submit", async () => {
    vi.mocked(register).mockResolvedValue(null);

    const wrapper = await mountForm();
    await wrapper.find("#registration-email").setValue("reader@dm.am");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    // Younger than the minimum fill time, as after a password manager.
    clock.now = 1900;

    await wrapper.find("#registration-password").setValue("correct horse");
    await acceptRules(wrapper);
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(wrapper.text()).not.toContain("Подождите");
    expect(register).not.toHaveBeenCalled();
    expect(wrapper.find("button.auth-button").attributes("aria-busy")).toBe(
      "true",
    );

    await new Promise((resolve) => setTimeout(resolve, 150));
    await flushPromises();

    expect(register).toHaveBeenCalledTimes(1);
    expect(
      wrapper.findComponent(RegistrationForm).emitted("success")?.[0],
    ).toEqual(["reader@dm.am", 30]);
  });

  it("says the one general sentence when the server named no reason", async () => {
    vi.mocked(register).mockResolvedValue(problem(409));

    const wrapper = await submitRegistration();

    expect(
      wrapper.findComponent(RegistrationForm).emitted("success"),
    ).toBeUndefined();
    expect(wrapper.find(".password-strength .warning-text").text()).toBe(
      "Не удалось выполнить запрос, попробуйте позже",
    );
  });
});
