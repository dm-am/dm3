/**
 * @vitest-environment jsdom
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { nextTick } from "vue";
import { accountApi } from "@/entities/user";
import {
  askAbout,
  control,
  resetRegistrationForm,
  type Wrapper,
} from "./RegistrationForm.fixtures";
import RegistrationForm from "./RegistrationForm.vue";
import { AuthDialogStub, problem } from "./authForms.fixtures";

vi.mock("@/entities/user", async () =>
  (await import("./authForms.doubles")).userEntity(),
);
vi.mock("@/shared/lib/composables/useToast", async () =>
  (await import("./authForms.doubles")).toastComposable(),
);

describe("RegistrationForm", () => {
  beforeEach(resetRegistrationForm);

  describe("a registration that was never finished", () => {
    const pending = () =>
      vi.mocked(accountApi.checkEmail).mockResolvedValue({
        data: { isAvailable: false, reason: "PendingActivation" },
        error: null,
      });

    // Not the default of the setting: the receipt says the hours the answer named.
    const resent = () =>
      vi.mocked(accountApi.recover).mockResolvedValue({
        data: {
          status: "ActivationResent",
          email: "reader@dm.am",
          linkLifetimeHours: 30,
        },
        error: null,
      } as never);

    const askForTheLetter = async (wrapper: Wrapper) => {
      await control(wrapper, "Отправить письмо повторно")?.trigger("click");
      await flushPromises();
    };

    it("offers the letter again and nothing else", async () => {
      pending();

      const wrapper = await askAbout();

      expect(wrapper.text()).toContain("Регистрация не завершена");
      expect(control(wrapper, "Отправить письмо повторно")).toBeDefined();
      expect(control(wrapper, "Войти")).toBeUndefined();
    });

    it("sends the letter itself and states the outcome on the same card", async () => {
      pending();
      resent();

      const wrapper = await askAbout();
      await askForTheLetter(wrapper);

      // Claimed at the press: the activation page sends the same request with no strip.
      expect(accountApi.recover).toHaveBeenCalledWith("reader@dm.am", {
        ownsRefusal: true,
        ownsRateLimit: true,
      });
      expect(
        wrapper.findComponent(RegistrationForm).emitted("openRecovery"),
      ).toBeUndefined();
      expect(wrapper.text()).toContain("Проверьте почту");
      expect(wrapper.text()).toContain(
        "Мы отправили повторное письмо со ссылкой для активации на",
      );
      expect(wrapper.text()).toContain("reader@dm.am");
      expect(wrapper.text()).toContain("Ссылка действительна 30 часов");
      expect(wrapper.find("#registration-email").exists()).toBe(false);
    });

    // The modal keeps the component mounted, so the receipt goes down with the form.
    it("takes the receipt down when the dialog closes", async () => {
      pending();
      resent();

      const wrapper = await askAbout();
      await askForTheLetter(wrapper);
      expect(wrapper.text()).toContain("Проверьте почту");

      // Emitted directly: the modal tier that raises it is stubbed.
      wrapper.findComponent(AuthDialogStub).vm.$emit("beforeClose");
      await nextTick();

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#registration-email").exists()).toBe(true);
    });

    it("does not send a second letter while the first is on the wire", async () => {
      pending();
      vi.mocked(accountApi.recover).mockImplementation(
        () => new Promise(() => {}) as never,
      );

      const wrapper = await askAbout();
      await askForTheLetter(wrapper);
      await askForTheLetter(wrapper);

      expect(accountApi.recover).toHaveBeenCalledTimes(1);
    });

    it("says the letter did not go out instead of promising one", async () => {
      pending();
      vi.mocked(accountApi.recover).mockResolvedValue({
        data: null,
        error: problem(500, "Ошибка сервера"),
      } as never);

      const wrapper = await askAbout();
      await askForTheLetter(wrapper);

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#registration-email-message").text()).toBe(
        "Ошибка сервера",
      );
    });

    it("promises no activation letter when another one went out", async () => {
      pending();
      vi.mocked(accountApi.recover).mockResolvedValue({
        data: { status: "PasswordResetSent", email: "reader@dm.am" },
        error: null,
      } as never);

      const wrapper = await askAbout();
      await askForTheLetter(wrapper);

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#registration-email-message").text()).toBe(
        "Не удалось отправить письмо",
      );
    });
  });
});
