/**
 * @vitest-environment jsdom
 */

/**
 * Switching the second factor on through the section that mounts the form:
 * the password that issues the secret, the code that confirms it, each refusal
 * under its field, and the copy of the secret in its own caption.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { accountApi } from "@/entities/user";
import { COPIED, COPY_FAILED } from "@/shared/lib/composables/useCopyFeedback";
import {
  issued,
  ON,
  resetSection,
  saidOutLoud,
  withStatus,
} from "./AccountTwoFactorSection.fixtures";

/** Stands in for the clipboard, which jsdom has none of. */
let write: ReturnType<typeof vi.fn>;

/** Opens the reveal and sends a password, the secret answered with `answer`. */
async function sendPassword(answer: unknown, password = "correct horse") {
  const wrapper = await withStatus({});
  await wrapper.find("button").trigger("click");
  vi.spyOn(accountApi, "setupTwoFactor").mockResolvedValue(answer as never);
  await wrapper.find("#two-factor-password").setValue(password);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
  return wrapper;
}

/** Sends a code for the issued secret, the confirmation answered with `answer`. */
async function sendCode(answer: unknown) {
  const wrapper = await issued();
  vi.spyOn(accountApi, "confirmTwoFactor").mockResolvedValue(answer as never);
  await wrapper.find("#two-factor-code").setValue("000000");
  await wrapper.find("form").trigger("submit");
  await flushPromises();
  return wrapper;
}

describe("the second-factor section", () => {
  beforeEach(() => {
    write = resetSection();
  });

  describe("switching the factor on", () => {
    it("puts a wrong password under the password field", async () => {
      const wrapper = await sendPassword(
        { data: null, error: { status: 400, title: "Неверный пароль" } },
        "wrong",
      );

      expect(wrapper.find(".form-field-error").text()).toBe("Неверный пароль");
      expect(wrapper.find("#two-factor-code").exists()).toBe(false);
    });

    /** A refusal is said under the password field, which still holds the password. */
    it("says a 500 on the password under the field", async () => {
      const wrapper = await sendPassword({
        data: null,
        error: { status: 500, title: "Ошибка сервера" },
      });

      expect(wrapper.find(".form-field-error").text()).toBe("Ошибка сервера");
      // No secret was issued, so the code field is shut.
      expect(wrapper.find("#two-factor-code").exists()).toBe(false);
    });

    // An expired session and a dead connection are the plate's.
    it.each([0, 401])(
      "leaves a %i on the password to the plate",
      async (status) => {
        const wrapper = await sendPassword({
          data: null,
          error: { status, title: "" },
        });

        expect(wrapper.find(".form-field-error").exists()).toBe(false);
        expect(wrapper.find("#two-factor-code").exists()).toBe(false);
      },
    );

    // A conflict: the factor is on already, so the form closes and the state is read again.
    it("re-reads the state when the factor turns out to be on already", async () => {
      const wrapper = await withStatus({});
      await wrapper.find("button").trigger("click");

      vi.spyOn(accountApi, "setupTwoFactor").mockResolvedValue({
        data: null,
        error: { status: 409, title: "Уже включена" },
      } as never);
      const read = vi
        .spyOn(accountApi, "getTwoFactorStatus")
        .mockResolvedValue({ data: { resource: ON }, error: null } as never);

      await wrapper.find("#two-factor-password").setValue("correct horse");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      // Once on opening, once after the conflict.
      expect(read).toHaveBeenCalledTimes(2);
      expect(wrapper.text()).toContain("Двухфакторная аутентификация включена");
      expect(wrapper.find("#two-factor-password").exists()).toBe(false);
      expect(wrapper.find(".form-field-error").exists()).toBe(false);
    });

    // Without a camera the square is useless; the string beside it is what such a reader has.
    it("shows the secret as text next to the square", async () => {
      const wrapper = await issued();

      expect(wrapper.text()).toContain("JBSWY3DPEHPK3PXP");
      expect(wrapper.text()).toContain("Или введите секрет вручную");
      expect(wrapper.find("#two-factor-code").exists()).toBe(true);
    });

    // Nothing on screen changes on a copy, so the pressed button says it.
    it("says the secret is copied in the button that copied it", async () => {
      const wrapper = await issued();

      const copy = wrapper.find(".copy-btn");
      await copy.trigger("click");
      await flushPromises();

      expect(write).toHaveBeenCalledWith("JBSWY3DPEHPK3PXP");
      expect(copy.text()).toBe(COPIED);
      expect(saidOutLoud()).toEqual([]);
    });

    // Told nothing, a reader pastes whatever was in the clipboard before.
    it("says so in the same button when the clipboard refused", async () => {
      write.mockRejectedValue(new Error("not allowed"));
      const wrapper = await issued();

      const copy = wrapper.find(".copy-btn");
      await copy.trigger("click");
      await flushPromises();

      expect(copy.text()).toBe(COPY_FAILED);
      expect(saidOutLoud()).toEqual([]);
    });

    it("puts a code that did not match under the code field", async () => {
      const wrapper = await sendCode({
        data: null,
        error: { status: 400, title: "Код не подошел" },
      });

      expect(wrapper.text()).toContain("Код не подошел");
      expect(wrapper.find(".codes-block").exists()).toBe(false);
    });

    // The same rule under the code field.
    it("says a 500 on the code under the field", async () => {
      const wrapper = await sendCode({
        data: null,
        error: { status: 500, title: "Ошибка сервера" },
      });

      expect(wrapper.find(".form-field-error").text()).toBe("Ошибка сервера");
      // The factor is not on: no codes were handed over.
      expect(wrapper.find(".codes-block").exists()).toBe(false);
    });
  });
});
