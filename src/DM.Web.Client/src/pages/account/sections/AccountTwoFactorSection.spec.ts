/**
 * @vitest-environment jsdom
 */

/**
 * The account block that switches the second factor on, reports on it and
 * switches it off.
 *
 * Three properties are worth a gate here, and each one is a way the screen can
 * lie about an account's own lock.
 *
 * The state that could not be read is not the state "off". Offering "Включить"
 * over a failed read promises an operation the server may already refuse as
 * "уже включен", and the reply would land under a password field where there
 * is nothing to correct.
 *
 * The set of recovery codes exists in one answer and nowhere else, so the block
 * that shows it may not be dismissible by accident: the way out is a checkbox
 * and then a button, and until both, the codes stay.
 *
 * A refusal goes under the field it is about. The server names a wrong password
 * ("password") and answers every failed second factor with one unnamed
 * sentence; put under the password field, that sentence sends the reader to
 * retype a password that was right.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import AccountTwoFactorSection from "./AccountTwoFactorSection.vue";
import type { TwoFactorStatus } from "@/shared/api/models/account";

const options = () => ({
  global: { plugins: [createPinia()] },
});

const OFF: TwoFactorStatus = {
  enabled: false,
  recoveryCodesLeft: 0,
  required: false,
  privilegeWithheld: false,
};

const ON: TwoFactorStatus = {
  enabled: true,
  enabledUtc: "2026-05-01T10:00:00Z",
  lastVerifiedUtc: "2026-08-20T18:30:00Z",
  recoveryCodesLeft: 7,
  required: false,
  privilegeWithheld: false,
};

/** Mounts the section over one state of the factor. */
async function withStatus(status: Partial<TwoFactorStatus>, base = OFF) {
  vi.spyOn(accountApi, "getTwoFactorStatus").mockResolvedValue({
    data: { resource: { ...base, ...status } },
    error: null,
  } as never);

  const wrapper = mount(AccountTwoFactorSection, options());
  await flushPromises();
  return wrapper;
}

const codes = () =>
  Array.from({ length: 10 }, (_, index) => `ABCD${index}EFGHIJKLMNO`);

describe("the second-factor section", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  describe("what it shows", () => {
    it("offers to switch the factor on when it is off", async () => {
      const wrapper = await withStatus({});

      expect(wrapper.text()).toContain("Второй фактор выключен");
      expect(wrapper.text()).toContain("Включить");
    });

    it("names when the factor was switched on and how many codes are left", async () => {
      const wrapper = await withStatus({}, ON);

      expect(wrapper.text()).toContain("Второй фактор включен");
      expect(wrapper.text()).toContain("01.05.2026");
      expect(wrapper.text()).toContain("Резервных кодов осталось:");
      expect(wrapper.text()).toContain("7");
    });

    // Colour alone would not carry this: the row says it in words.
    it("warns in words when the codes are nearly gone", async () => {
      const wrapper = await withStatus({ recoveryCodesLeft: 2 }, ON);

      expect(wrapper.text()).toContain("Резервных кодов почти не осталось");
    });

    it("shows a removal somebody asked for from a mailbox", async () => {
      const wrapper = await withStatus(
        { removalDueUtc: "2026-09-01T09:00:00Z" },
        ON,
      );

      expect(wrapper.text()).toContain("Снятие второго фактора назначено на");
      expect(wrapper.text()).toContain("01.09.2026");
    });

    it("does not offer to switch anything on when the state could not be read", async () => {
      vi.spyOn(accountApi, "getTwoFactorStatus").mockResolvedValue({
        data: null,
        error: { status: 500, title: "Ошибка сервера" },
      } as never);

      const wrapper = mount(AccountTwoFactorSection, options());
      await flushPromises();

      expect(wrapper.find(".status-card--failed").exists()).toBe(true);
      expect(wrapper.text()).not.toContain("Второй фактор выключен");
    });
  });

  describe("switching the factor on", () => {
    /** Opens the reveal and gets as far as the issued secret. */
    async function issued() {
      const wrapper = await withStatus({});
      await wrapper.find("button").trigger("click");

      vi.spyOn(accountApi, "setupTwoFactor").mockResolvedValue({
        data: {
          resource: {
            secret: "JBSWY3DPEHPK3PXP",
            otpAuthUri: "otpauth://totp/DM:reader?secret=JBSWY3DPEHPK3PXP",
          },
        },
        error: null,
      } as never);

      await wrapper.find("#two-factor-password").setValue("correct horse");
      await wrapper.find("form").trigger("submit");
      await flushPromises();
      return wrapper;
    }

    it("puts a wrong password under the password field", async () => {
      const wrapper = await withStatus({});
      await wrapper.find("button").trigger("click");

      vi.spyOn(accountApi, "setupTwoFactor").mockResolvedValue({
        data: null,
        error: { status: 400, title: "Неверный пароль" },
      } as never);

      await wrapper.find("#two-factor-password").setValue("wrong");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(wrapper.find(".form-field-error").text()).toBe("Неверный пароль");
      expect(wrapper.find("#two-factor-code").exists()).toBe(false);
    });

    // Without a camera the square is useless, and the string beside it is the
    // whole of what such a reader has.
    it("shows the secret as text next to the square", async () => {
      const wrapper = await issued();

      expect(wrapper.text()).toContain("JBSWY3DPEHPK3PXP");
      expect(wrapper.text()).toContain("Или введите секрет вручную");
      expect(wrapper.find("#two-factor-code").exists()).toBe(true);
    });

    it("puts a code that did not match under the code field", async () => {
      const wrapper = await issued();

      vi.spyOn(accountApi, "confirmTwoFactor").mockResolvedValue({
        data: null,
        error: { status: 400, title: "Код не подошел" },
      } as never);

      await wrapper.find("#two-factor-code").setValue("000000");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(wrapper.text()).toContain("Код не подошел");
      expect(wrapper.find(".codes-block").exists()).toBe(false);
    });

    it("hands over the recovery codes and holds them until they are saved", async () => {
      const wrapper = await issued();

      vi.spyOn(accountApi, "confirmTwoFactor").mockResolvedValue({
        data: { resource: { codes: codes() } },
        error: null,
      } as never);
      vi.spyOn(accountApi, "getTwoFactorStatus").mockResolvedValue({
        data: { resource: ON },
        error: null,
      } as never);

      await wrapper.find("#two-factor-code").setValue("123456");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      const block = wrapper.find(".codes-block");
      expect(block.exists()).toBe(true);
      expect(block.text()).toContain("Коды показываются один раз");
      expect(wrapper.findAll(".code-item")).toHaveLength(10);
      // Grouped by four, the way the server strips them back out again.
      expect(wrapper.find(".code-item").text()).toBe("ABCD-0EFG-HIJK-LMNO");

      // "Готово" is closed until the checkbox says the codes were written down.
      const done = wrapper
        .findAll("button")
        .find((button) => button.text() === "Готово");
      expect(done?.attributes("disabled")).toBeDefined();

      await wrapper.find(".codes-saved input").setValue(true);
      await done?.trigger("click");
      await flushPromises();

      expect(wrapper.find(".codes-block").exists()).toBe(false);
      expect(wrapper.text()).toContain("Второй фактор включен");
    });

    // The re-read of the state runs right after, and it can fail. Decided by
    // that read, the block would have shown "не удалось узнать состояние" over
    // a factor that is now on, and the ten codes that exist in one answer and
    // nowhere else would be gone with it.
    it("keeps the codes on screen when the state could not be re-read", async () => {
      const wrapper = await issued();

      vi.spyOn(accountApi, "confirmTwoFactor").mockResolvedValue({
        data: { resource: { codes: codes() } },
        error: null,
      } as never);
      vi.spyOn(accountApi, "getTwoFactorStatus").mockResolvedValue({
        data: null,
        error: { status: 500, title: "Ошибка сервера" },
      } as never);

      await wrapper.find("#two-factor-code").setValue("123456");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(wrapper.findAll(".code-item")).toHaveLength(10);
      expect(wrapper.find(".status-card--failed").exists()).toBe(false);
    });
  });

  describe("switching the factor off", () => {
    /** Opens the password-and-code pair behind "Отключить". */
    async function disabling() {
      const wrapper = await withStatus({}, ON);
      const trigger = wrapper
        .findAll("button")
        .find((button) => button.text() === "Отключить");
      await trigger?.trigger("click");

      await wrapper.find("#two-factor-action-password").setValue("correct");
      await wrapper.find("#two-factor-action-code").setValue("123456");
      return wrapper;
    }

    it("puts a wrong password under the password field", async () => {
      const wrapper = await disabling();

      vi.spyOn(accountApi, "disableTwoFactor").mockResolvedValue({
        data: null,
        error: { status: 400, errors: { password: ["Неверный пароль"] } },
      } as never);

      await wrapper.find(".confirm-form").trigger("submit");
      await flushPromises();

      expect(wrapper.find("#two-factor-action-password").attributes("id")).toBe(
        "two-factor-action-password",
      );
      expect(wrapper.text()).toContain("Неверный пароль");
      expect(wrapper.text()).toContain("Второй фактор включен");
    });

    // The refusal names no field, and under the password it would send the
    // reader to retype a password the server accepted.
    it("puts a refused code under the code field", async () => {
      const wrapper = await disabling();

      vi.spyOn(accountApi, "disableTwoFactor").mockResolvedValue({
        data: null,
        error: { status: 400, title: "Код не подошел" },
      } as never);

      await wrapper.find(".confirm-form").trigger("submit");
      await flushPromises();

      const errors = wrapper
        .findAll(".form-field-error")
        .map((node) => node.text());
      expect(errors).toEqual(["Код не подошел"]);
    });

    it("re-reads the state once the factor is off", async () => {
      const wrapper = await disabling();

      vi.spyOn(accountApi, "disableTwoFactor").mockResolvedValue({
        data: null,
        error: null,
      } as never);
      vi.spyOn(accountApi, "getTwoFactorStatus").mockResolvedValue({
        data: { resource: OFF },
        error: null,
      } as never);

      await wrapper.find(".confirm-form").trigger("submit");
      await flushPromises();

      expect(wrapper.text()).toContain("Второй фактор выключен");
    });
  });

  describe("reissuing the codes", () => {
    it("shows the new set and says the old one is dead", async () => {
      const wrapper = await withStatus({}, ON);
      const trigger = wrapper
        .findAll("button")
        .find((button) => button.text() === "Перевыпустить коды");
      await trigger?.trigger("click");

      vi.spyOn(accountApi, "reissueRecoveryCodes").mockResolvedValue({
        data: { resource: { codes: codes() } },
        error: null,
      } as never);

      await wrapper.find("#two-factor-action-password").setValue("correct");
      await wrapper.find("#two-factor-action-code").setValue("123456");
      await wrapper.find(".confirm-form").trigger("submit");
      await flushPromises();

      expect(wrapper.text()).toContain("Новые резервные коды");
      expect(wrapper.text()).toContain(
        "Прежний набор кодов больше не работает",
      );
      expect(wrapper.findAll(".code-item")).toHaveLength(10);
    });
  });
});
