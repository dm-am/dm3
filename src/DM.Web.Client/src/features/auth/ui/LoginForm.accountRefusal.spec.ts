/**
 * @vitest-environment jsdom
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import type { GeneralError } from "@/shared/api/models/common";
import { signIn, completeSecondFactor } from "@/entities/user";
import { push, toast } from "./authForms.doubles";
import {
  atSecondFactor,
  linkBeside,
  mountForm,
  refused,
  resetLoginForm,
  submitCredentials,
  typeCode,
} from "./LoginForm.fixtures";
import { problem } from "./authForms.fixtures";

vi.mock("@/entities/user", async () =>
  (await import("./authForms.doubles")).userEntity(),
);
vi.mock("vue-router", async () =>
  (await import("./authForms.doubles")).vueRouter(),
);
vi.mock("@/shared/lib/composables/useToast", async () =>
  (await import("./authForms.doubles")).toastComposable(),
);

// Mocked, not stubbed: AuthDialog imports Dialog.vue directly.
vi.mock("vue-final-modal", async () =>
  (await import("@/shared/lib/testing/vueFinalModalStub")).vueFinalModal(),
);

enableAutoUnmount(afterEach);

describe("LoginForm", () => {
  beforeEach(resetLoginForm);

  describe("an account the server refused", () => {
    type Banned = GeneralError & { banExpiresUtc?: string | null };

    const banned = (banExpiresUtc?: string | null): Banned => {
      const failure = problem(403, "Аккаунт забанен");
      return banExpiresUtc === undefined
        ? failure
        : { ...failure, banExpiresUtc };
    };

    const removed = () => problem(403, "Аккаунт удален");

    it("spells the ban out in the strip, and not in a box of its own", async () => {
      vi.mocked(signIn).mockResolvedValue({
        stage: "refused",
        failure: banned(),
      });

      const wrapper = await submitCredentials();

      const strip = wrapper.find("#login-password-message");
      expect(strip.text()).toBe("Аккаунт находится в полном бане бессрочно");
      expect(strip.classes()).toContain("auth-message--error");
      expect(wrapper.find(".auth-warnbox").exists()).toBe(false);
      expect(toast.error).not.toHaveBeenCalled();
    });

    it("names the day the ban runs out when the answer carries one", async () => {
      vi.mocked(signIn).mockResolvedValue({
        stage: "refused",
        failure: banned("2027-02-28T00:00:00Z"),
      });

      const wrapper = await submitCredentials();

      expect(wrapper.find("#login-password-message").text()).toBe(
        "Аккаунт находится в полном бане до 28.02.2027",
      );
    });

    it("says a ban with no end is indefinite", async () => {
      vi.mocked(signIn).mockResolvedValue({
        stage: "refused",
        failure: banned(null),
      });

      const wrapper = await submitCredentials();

      expect(wrapper.find("#login-password-message").text()).toBe(
        "Аккаунт находится в полном бане бессрочно",
      );
    });

    it("names the removal in the strip and offers the way back beside it", async () => {
      vi.mocked(signIn).mockResolvedValue({
        stage: "refused",
        failure: removed(),
      });

      const wrapper = await submitCredentials();

      expect(wrapper.find("#login-password-message").text()).toBe(
        "Аккаунт с этой почтой был удален",
      );
      expect(linkBeside(wrapper, "Пароль").text()).toBe("Восстановить аккаунт");
    });

    // Every further code would spend an attempt against a door shut for another reason.
    it("answers the code step in the same words, and stops asking", async () => {
      vi.mocked(signIn).mockResolvedValue({
        stage: "refused",
        failure: banned(null),
      });
      const first = await submitCredentials();
      const said = first.find("#login-password-message").text();

      const second = await atSecondFactor();
      vi.mocked(completeSecondFactor).mockResolvedValue(banned(null));
      await typeCode(second, "123456");

      expect(second.find("#two-factor-code-message").text()).toBe(said);
      expect(second.find(".auth-button").attributes("disabled")).toBeDefined();

      await typeCode(second, "654321");
      expect(completeSecondFactor).toHaveBeenCalledTimes(1);
    });

    it("names the removal on the code step and offers the way back", async () => {
      const wrapper = await atSecondFactor();
      vi.mocked(completeSecondFactor).mockResolvedValue(removed());

      await typeCode(wrapper, "123456");

      expect(wrapper.find("#two-factor-code-message").text()).toBe(
        "Аккаунт с этой почтой был удален",
      );
      expect(linkBeside(wrapper, "Код из приложения").text()).toBe(
        "Восстановить аккаунт",
      );
      expect(wrapper.find(".auth-button").attributes("disabled")).toBe("");
    });

    it("drops the refusal as soon as the address is edited", async () => {
      vi.mocked(signIn).mockResolvedValue({
        stage: "refused",
        failure: banned(null),
      });

      const wrapper = await submitCredentials();
      const strip = () => wrapper.find("#login-password-message").text();
      expect(strip()).toBe("Аккаунт находится в полном бане бессрочно");

      await wrapper.find("#email").setValue("second@dm.am");

      expect(strip()).toBe("");
    });
  });

  describe("the way back for a removed account", () => {
    const refuseAsRemoved = async () => {
      vi.mocked(signIn).mockResolvedValue(refused(403, "Аккаунт удален"));
      return submitCredentials();
    };

    it("is not offered to a reader who has not been refused", async () => {
      const wrapper = mountForm();
      await flushPromises();

      expect(linkBeside(wrapper, "Пароль").text()).toBe("Не могу войти");
      expect(wrapper.text()).not.toContain("Восстановить аккаунт");
    });

    it("takes the place of the standing way out when the account is gone", async () => {
      const wrapper = await refuseAsRemoved();

      expect(wrapper.find("#login-password-message").text()).toBe(
        "Аккаунт с этой почтой был удален",
      );
      expect(linkBeside(wrapper, "Пароль").text()).toBe("Восстановить аккаунт");
    });

    it("is not offered to an account that was banned", async () => {
      vi.mocked(signIn).mockResolvedValue(refused(403, "Аккаунт забанен"));

      const wrapper = await submitCredentials();

      expect(linkBeside(wrapper, "Пароль").text()).toBe("Не могу войти");
    });

    it("goes away as soon as the address is edited", async () => {
      const wrapper = await refuseAsRemoved();

      await wrapper.find("#email").setValue("lexx@dm.am");
      await flushPromises();

      expect(linkBeside(wrapper, "Пароль").text()).toBe("Не могу войти");
    });

    it("closes the dialog and opens support on the restoration category", async () => {
      const wrapper = await refuseAsRemoved();

      await linkBeside(wrapper, "Пароль").trigger("click");
      await flushPromises();

      expect(wrapper.emitted("cancel")).toHaveLength(1);
      expect(push).toHaveBeenCalledWith("/support?subtype=AccountRestoration");
    });
  });
});
