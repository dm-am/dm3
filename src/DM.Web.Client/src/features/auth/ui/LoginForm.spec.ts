/**
 * @vitest-environment jsdom
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { nextTick } from "vue";
import { signIn, completeSecondFactor } from "@/entities/user";
import { AuthDialog } from "@/shared/ui/Layout";
import { toast } from "./authForms.doubles";
import { expectWaitRunsDown, problem } from "./authForms.fixtures";
import LoginForm from "./LoginForm.vue";
import {
  atSecondFactor,
  mountForm,
  refused,
  resetLoginForm,
  submitCredentials,
  typeCode,
  wrongPair,
} from "./LoginForm.fixtures";

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

  it("shows the reason the server refused instead of reporting success", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(403, "Аккаунт забанен"));

    const wrapper = await submitCredentials();

    expect(wrapper.findComponent(LoginForm).emitted("success")).toBeUndefined();
    expect(wrapper.text()).toContain(
      "Аккаунт находится в полном бане бессрочно",
    );
  });

  it("says how long the lockout has left, and shuts the button with it", async () => {
    vi.mocked(signIn).mockResolvedValue(
      refused(403, "Вход заблокирован", 1800),
    );

    const wrapper = await submitCredentials();

    const strip = wrapper.find("#login-password-message");
    expect(strip.text()).toBe(
      "Слишком много попыток, попробуйте через 30 минут",
    );
    // The same counter as the rate limit, so the button is shut too.
    expect(wrapper.find("button.auth-button").attributes("disabled")).toBe("");

    // And nothing is said twice: the request took the refusal over.
    expect(toast.warning).not.toHaveBeenCalled();
  });

  it("falls back to the wordless wait when the lockout brings no seconds", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(403, "Вход заблокирован"));

    const wrapper = await submitCredentials();

    expect(wrapper.find("#login-password-message").text()).toBe(
      "Слишком много попыток, попробуйте позже",
    );
  });

  it("leaves a refusal it does not recognise in the server's own words", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(403, "Вход недоступен"));

    const wrapper = await submitCredentials();

    expect(wrapper.find("#login-password-message").text()).toBe(
      "Вход недоступен",
    );
  });

  it("keeps showing what a rejected form got wrong", async () => {
    vi.mocked(signIn).mockResolvedValue(wrongPair());

    const wrapper = await submitCredentials();

    expect(wrapper.findComponent(LoginForm).emitted("success")).toBeUndefined();
    expect(wrapper.text()).toContain("Неверная почта или пароль");
  });

  it("says what is wrong under the field and leaves the field itself alone", async () => {
    vi.mocked(signIn).mockResolvedValue(wrongPair());

    const wrapper = await submitCredentials();

    const message = wrapper.find("#login-password-message");
    expect(message.classes()).toContain("auth-message--error");
    expect(message.text()).toBe("Неверная почта или пароль");
    // Nothing on the field or its wrapper carries the state.
    expect(wrapper.findAll(".auth-field")[1].classes()).toEqual(["auth-field"]);
    expect(wrapper.find("#password").attributes("aria-invalid")).toBe("true");
  });

  it("says a server error under the password, and not in a plate", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(500, "Ошибка сервера"));

    const wrapper = await submitCredentials();

    expect(wrapper.findComponent(LoginForm).emitted("success")).toBeUndefined();
    expect(wrapper.find("#login-password-message").text()).toBe(
      "Ошибка сервера",
    );
  });

  it("leaves a request that never arrived to its plate", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(0, ""));

    const wrapper = await submitCredentials();

    expect(wrapper.findComponent(LoginForm).emitted("success")).toBeUndefined();
    expect(wrapper.find("#login-password-message").text()).toBe("");
  });

  describe("a rate limit on the way in", () => {
    beforeEach(() => {
      // Only the interval: Date.now is already controlled by the fixtures.
      vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it("stands under the password, shuts the button, and runs down", async () => {
      vi.mocked(signIn).mockResolvedValue(
        refused(429, "Слишком много запросов", 3),
      );

      const wrapper = await submitCredentials();

      expect(toast.warning).not.toHaveBeenCalled();
      await expectWaitRunsDown(wrapper, "#login-password-message");
    });

    it("stands under the code as well, and holds the check back", async () => {
      const wrapper = await atSecondFactor();
      vi.mocked(completeSecondFactor).mockResolvedValue(
        problem(429, "Слишком много запросов", 2),
      );

      await typeCode(wrapper, "123456");

      const strip = wrapper.find("#two-factor-code-message");
      expect(strip.text()).toBe(
        "Слишком много попыток, попробуйте через 2 секунды",
      );
      expect(wrapper.find("button.auth-button").attributes("disabled")).toBe(
        "",
      );
      expect(toast.warning).not.toHaveBeenCalled();
      expect(vi.mocked(completeSecondFactor)).toHaveBeenCalledTimes(1);

      // Retyping the code during the wait sends nothing.
      await typeCode(wrapper, "654321");
      expect(vi.mocked(completeSecondFactor)).toHaveBeenCalledTimes(1);

      vi.advanceTimersByTime(2000);
      await nextTick();
      expect(strip.text()).toBe("Шесть цифр из приложения-аутентификатора");
      expect(
        wrapper.find("button.auth-button").attributes("disabled"),
      ).toBeUndefined();
    });

    it("says the wait is unknown when the server named none", async () => {
      vi.mocked(signIn).mockResolvedValue(
        refused(429, "Слишком много запросов"),
      );

      const wrapper = await submitCredentials();

      expect(wrapper.find("#login-password-message").text()).toBe(
        "Слишком много попыток, попробуйте позже",
      );
      expect(
        wrapper.find("button.auth-button").attributes("disabled"),
      ).toBeUndefined();
    });

    // The modal keeps the component mounted, so a running interval would outlive it.
    it("drops the wait when the dialog closes", async () => {
      vi.mocked(signIn).mockResolvedValue(
        refused(429, "Слишком много запросов", 30),
      );

      const wrapper = await submitCredentials();
      expect(vi.getTimerCount()).toBe(1);

      // Emitted directly: the modal tier that raises it is mocked.
      wrapper.findComponent(AuthDialog).vm.$emit("beforeClose");
      await nextTick();

      expect(vi.getTimerCount()).toBe(0);
      expect(wrapper.find("#login-password-message").text()).toBe("");
    });

    it("drops the wait when the form leaves the page", async () => {
      vi.mocked(signIn).mockResolvedValue(
        refused(429, "Слишком много запросов", 30),
      );

      const wrapper = await submitCredentials();
      expect(vi.getTimerCount()).toBe(1);

      wrapper.unmount();

      expect(vi.getTimerCount()).toBe(0);
    });
  });

  it("reports success once the session is established", async () => {
    vi.mocked(signIn).mockResolvedValue({ stage: "signedIn" });

    const wrapper = await submitCredentials();

    expect(wrapper.findComponent(LoginForm).emitted("success")).toHaveLength(1);
  });

  describe("the address", () => {
    it("is not objected to while it is still empty", async () => {
      const wrapper = await mountForm();
      await flushPromises();

      await wrapper.find("#email").trigger("blur");
      await flushPromises();

      expect(wrapper.find("#login-email-message").text()).toBe("");
    });

    it("is objected to for its shape, under the field", async () => {
      const wrapper = await mountForm();
      await flushPromises();

      await wrapper.find("#email").setValue("lex@");
      await wrapper.find("#email").trigger("blur");
      await flushPromises();

      expect(wrapper.find("#login-email-message").text()).toBe(
        "Неверный формат почты",
      );
    });

    it("is not sent while its shape is wrong", async () => {
      const wrapper = await submitCredentials("lex@");

      expect(signIn).not.toHaveBeenCalled();
      expect(wrapper.find("#login-email-message").text()).toBe(
        "Неверный формат почты",
      );
    });

    it("is sent as it was typed, and answered as credentials", async () => {
      vi.mocked(signIn).mockResolvedValue(wrongPair());

      const wrapper = await submitCredentials("lex@dm.am");

      expect(signIn).toHaveBeenCalledWith(
        expect.objectContaining({ email: "lex@dm.am" }),
      );
      expect(wrapper.find("#login-email-message").text()).toBe("");
      expect(wrapper.find("#login-password-message").text()).toBe(
        "Неверная почта или пароль",
      );
    });
  });

  describe("the open dialog", () => {
    it("puts the caret in the first field that is empty", async () => {
      const wrapper = await mountForm();
      await flushPromises();

      expect(document.activeElement).toBe(wrapper.find("#email").element);
    });

    it("skips the address it was handed", async () => {
      const wrapper = await mountForm("lex@dm.am");
      await flushPromises();

      expect(document.activeElement).toBe(wrapper.find("#password").element);
    });
  });
});
