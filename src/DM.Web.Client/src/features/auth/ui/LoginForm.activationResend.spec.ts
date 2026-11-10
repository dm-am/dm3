/**
 * @vitest-environment jsdom
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { nextTick } from "vue";
import type { BadRequestError } from "@/shared/api/models/common";
import { signIn } from "@/entities/user";
import { AuthDialog } from "@/shared/ui/Layout";
import { recover } from "./authForms.doubles";
import {
  linkBeside,
  resetLoginForm,
  submitCredentials,
} from "./LoginForm.fixtures";
import { problem } from "./authForms.fixtures";

vi.mock("@/entities/user", async () =>
  (await import("./authForms.doubles")).userEntity(),
);
vi.mock("@/shared/lib/composables/useToast", async () =>
  (await import("./authForms.doubles")).toastComposable(),
);

// Mocked, not stubbed: AuthDialog imports Dialog.vue directly; the page keeps the real container.
vi.mock("vue-final-modal", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-final-modal")>()),
  ...(await import("@/shared/lib/testing/vueFinalModalStub")).vueFinalModal(),
}));

enableAutoUnmount(afterEach);

describe("LoginForm", () => {
  beforeEach(resetLoginForm);

  describe("an unfinished registration", () => {
    const pending: BadRequestError = {
      ...problem(400),
      errors: { _pendingActivation: ["true"] },
    };

    const signInPending = () =>
      vi.mocked(signIn).mockResolvedValue({
        stage: "refused",
        failure: pending,
      });

    // Not the default of the setting: the receipt says the hours the answer named.
    const resent = () =>
      recover.mockResolvedValue({
        data: {
          status: "ActivationResent",
          email: "reader@dm.am",
          linkLifetimeHours: 30,
        },
        error: null,
      });

    const askForTheLetter = async (
      wrapper: Awaited<ReturnType<typeof submitCredentials>>,
    ) => {
      await linkBeside(wrapper, "Пароль").trigger("click");
      await flushPromises();
    };

    it("offers the letter again in place of the way out", async () => {
      signInPending();

      const wrapper = await submitCredentials();

      expect(wrapper.text()).toContain("Регистрация не завершена");
      expect(linkBeside(wrapper, "Пароль").text()).toBe(
        "Отправить письмо повторно",
      );
    });

    it("sends the letter itself and states the outcome on the same card", async () => {
      signInPending();
      resent();

      const wrapper = await submitCredentials();
      await askForTheLetter(wrapper);

      // Claimed at the press: the activation page sends the same request with no strip.
      expect(recover).toHaveBeenCalledWith("reader@dm.am", {
        ownsRefusal: true,
        ownsRateLimit: true,
      });
      expect(wrapper.text()).toContain("Проверьте почту");
      expect(wrapper.text()).toContain(
        "Мы отправили повторное письмо со ссылкой для активации на",
      );
      expect(wrapper.text()).toContain("reader@dm.am");
      expect(wrapper.text()).toContain("Ссылка действительна 30 часов");
      expect(wrapper.find("#password").exists()).toBe(false);
    });

    // The modal keeps the component mounted, so the receipt is taken down by hand.
    it("takes the receipt down when the dialog closes", async () => {
      signInPending();
      resent();

      const wrapper = await submitCredentials();
      await askForTheLetter(wrapper);
      expect(wrapper.text()).toContain("Проверьте почту");

      // Emitted directly: the modal tier that raises it is mocked.
      wrapper.findComponent(AuthDialog).vm.$emit("beforeClose");
      await nextTick();

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#password").exists()).toBe(true);
    });

    it("does not send a second letter while the first is on the wire", async () => {
      signInPending();
      recover.mockImplementation(() => new Promise(() => {}));

      const wrapper = await submitCredentials();
      await askForTheLetter(wrapper);
      await askForTheLetter(wrapper);

      expect(recover).toHaveBeenCalledTimes(1);
    });

    it("says the letter did not go out instead of promising one", async () => {
      signInPending();
      recover.mockResolvedValue({
        data: null,
        error: problem(500, "Ошибка сервера"),
      });

      const wrapper = await submitCredentials();
      await askForTheLetter(wrapper);

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#login-password-message").text()).toBe(
        "Ошибка сервера",
      );
    });

    it("promises no activation letter when another one went out", async () => {
      signInPending();
      recover.mockResolvedValue({
        data: { status: "PasswordResetSent", email: "reader@dm.am" },
        error: null,
      });

      const wrapper = await submitCredentials();
      await askForTheLetter(wrapper);

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#login-password-message").text()).toBe(
        "Не удалось отправить письмо",
      );
    });

    it("goes back to the ordinary way out as soon as anything is typed", async () => {
      signInPending();

      const wrapper = await submitCredentials();
      await wrapper.find("#email").setValue("reader@dm.ru");

      expect(linkBeside(wrapper, "Пароль").text()).toBe("Не могу войти");
    });
  });
});
