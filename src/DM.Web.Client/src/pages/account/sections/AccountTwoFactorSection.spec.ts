/**
 * @vitest-environment jsdom
 */

/**
 * The account block of the second factor: a state that could not be read is
 * not "off"; the codes stay until a box and a button release them; a refusal
 * goes under the field it is about; a copy answers in its own caption; a
 * failed read is the card's, worded by the server, but for the two statuses
 * the interceptor speaks for. Switching on is the enabling spec's.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { COPIED } from "@/shared/lib/composables/useCopyFeedback";
import AccountTwoFactorSection from "./AccountTwoFactorSection.vue";
import {
  codes,
  confirmed,
  OFF,
  ON,
  options,
  resetSection,
  saidOutLoud,
  withStatus,
} from "./AccountTwoFactorSection.fixtures";

/** Stands in for the clipboard, which jsdom has none of. */
let write: ReturnType<typeof vi.fn>;

describe("the second-factor section", () => {
  beforeEach(() => {
    write = resetSection();
  });

  describe("what it shows", () => {
    it("offers to switch the factor on when it is off", async () => {
      const wrapper = await withStatus({});

      expect(wrapper.text()).toContain(
        "Двухфакторная аутентификация выключена",
      );
      expect(wrapper.text()).toContain("Включить");
    });

    it("names when the factor was switched on and how many codes are left", async () => {
      const wrapper = await withStatus({}, ON);

      expect(wrapper.text()).toContain("Двухфакторная аутентификация включена");
      expect(wrapper.text()).toContain("01.05.2026");
      expect(wrapper.text()).toContain("Резервных кодов осталось:");
      expect(wrapper.text()).toContain("7");
    });

    // Said in words, not only in colour.
    it("warns in words when the codes are nearly gone", async () => {
      const wrapper = await withStatus({ recoveryCodesLeft: 2 }, ON);

      expect(wrapper.text()).toContain("Резервных кодов почти не осталось");
    });

    it("shows a removal somebody asked for from a mailbox", async () => {
      const wrapper = await withStatus(
        { removalDueUtc: "2026-09-01T09:00:00Z" },
        ON,
      );

      expect(wrapper.text()).toContain(
        "Отключение двухфакторной аутентификации назначено на",
      );
      expect(wrapper.text()).toContain("01.09.2026");
    });

    it("does not offer to switch anything on when the state could not be read", async () => {
      vi.spyOn(accountApi, "getTwoFactorStatus").mockResolvedValue({
        data: null,
        error: { status: 500, title: "Ошибка сервера" },
      } as never);

      const wrapper = mount(AccountTwoFactorSection, options());
      await flushPromises();

      expect(wrapper.find(".error-state").exists()).toBe(true);
      expect(wrapper.text()).not.toContain(
        "Двухфакторная аутентификация выключена",
      );
    });

    it("reads the state again on Повторить, with no reload of the page", async () => {
      const load = vi
        .spyOn(accountApi, "getTwoFactorStatus")
        .mockResolvedValueOnce({
          data: null,
          error: { status: 500, title: "Ошибка сервера" },
        } as never)
        .mockResolvedValue({
          data: { resource: { enabled: false } },
          error: null,
        } as never);

      const wrapper = mount(AccountTwoFactorSection, options());
      await flushPromises();
      await wrapper.get(".error-retry").trigger("click");
      await flushPromises();

      expect(load).toHaveBeenCalledTimes(2);
      expect(wrapper.find(".error-state").exists()).toBe(false);
    });

    it("stands a placeholder and no word while the state is read", async () => {
      vi.spyOn(accountApi, "getTwoFactorStatus").mockReturnValue(
        new Promise(() => {}) as never,
      );

      const wrapper = mount(AccountTwoFactorSection, options());
      await flushPromises();

      expect(wrapper.find(".account-skeleton").exists()).toBe(true);
      expect(wrapper.text()).not.toContain("Загрузка");
    });

    /** Mounts the section over a read the server refused. */
    async function failedRead(error: Record<string, unknown>) {
      vi.spyOn(accountApi, "getTwoFactorStatus").mockResolvedValue({
        data: null,
        error,
      } as never);

      const wrapper = mount(AccountTwoFactorSection, options());
      await flushPromises();
      return wrapper.get(".error-state");
    }

    /** The read takes the refusal and the limit over, checked at the call. */
    it("takes the refusal and the limit over on the read", async () => {
      const load = vi
        .spyOn(accountApi, "getTwoFactorStatus")
        .mockResolvedValue({ data: null, error: null } as never);

      mount(AccountTwoFactorSection, options());
      await flushPromises();

      expect(load).toHaveBeenCalledWith(
        expect.objectContaining({ ownsRefusal: true, ownsRateLimit: true }),
      );
    });

    /** The card prints what the server said; the way on stays the section's. */
    it.each([403, 500])(
      "prints what the server said about a %i",
      async (status) => {
        const card = await failedRead({ status, title: "Отключено для роли" });

        expect(card.text()).toContain("Отключено для роли");
        expect(card.text()).not.toContain("Не удалось узнать состояние");
        // The way on is the section's whatever the server said.
        expect(card.get(".error-retry").text()).toBe("Повторить");
      },
    );

    /** The wait is built from Retry-After: the title of a 429 has no number. */
    it("names the seconds when the limit refused the read", async () => {
      const card = await failedRead({
        status: 429,
        title: "Слишком много запросов",
        retryAfterSeconds: 30,
      });

      expect(card.text()).toContain(
        "Слишком много попыток, попробуйте через 30 секунд",
      );
      expect(card.text()).not.toContain("Слишком много запросов");
    });

    /** An ended session and a request that never left keep the section's line; the plate says why. */
    it.each([0, 401])("keeps its own line for a %i", async (status) => {
      const card = await failedRead({ status, title: "Unauthorized" });

      expect(card.text()).toContain(
        "Не удалось узнать состояние двухфакторной аутентификации",
      );
      expect(card.text()).not.toContain("Unauthorized");
    });

    // Nobody else speaks for this one, so the card relays it.
    it("relays a refusal the interceptor keeps quiet about", async () => {
      const card = await failedRead({
        status: 400,
        title: "Состояние недоступно",
      });

      expect(card.text()).toContain("Состояние недоступно");
    });
  });

  describe("switching the factor on", () => {
    it("hands over the recovery codes and holds them until they are saved", async () => {
      const wrapper = await confirmed();

      const block = wrapper.find(".codes-block");
      expect(block.exists()).toBe(true);
      expect(block.text()).toContain("Коды показываются один раз");
      expect(wrapper.findAll(".code-item")).toHaveLength(10);
      // Grouped by four, as the server strips them out again.
      expect(wrapper.find(".code-item").text()).toBe("ABCD-0EFG-HIJK-LMNO");

      // "Готово" is shut until the box says the codes were written down.
      const done = wrapper
        .findAll("button")
        .find((button) => button.text() === "Готово");
      expect(done?.attributes("disabled")).toBeDefined();

      await wrapper.find(".codes-saved input").setValue(true);
      await done?.trigger("click");
      await flushPromises();

      expect(wrapper.find(".codes-block").exists()).toBe(false);
      expect(wrapper.text()).toContain("Двухфакторная аутентификация включена");
    });

    // The clipboard gets the set the screen shows: grouped by four, one to a line.
    it("puts the whole set in the clipboard as the screen reads it", async () => {
      const wrapper = await confirmed();

      const copyAll = wrapper
        .findAll("button")
        .find((button) => button.text() === "Скопировать все");
      await copyAll?.trigger("click");
      await flushPromises();

      const onScreen = wrapper.findAll(".code-item").map((item) => item.text());
      expect(onScreen).toHaveLength(10);
      expect(write).toHaveBeenCalledWith(onScreen.join("\n"));
      expect(copyAll?.text()).toBe(COPIED);
      expect(saidOutLoud()).toEqual([]);
    });

    // A failed re-read after the confirmation keeps the codes on screen.
    it("keeps the codes on screen when the state could not be re-read", async () => {
      const wrapper = await confirmed({
        data: null,
        error: { status: 500, title: "Ошибка сервера" },
      });

      expect(wrapper.findAll(".code-item")).toHaveLength(10);
      expect(wrapper.find(".error-state").exists()).toBe(false);
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

      expect(wrapper.text()).toContain("Неверный пароль");
      expect(wrapper.text()).toContain("Двухфакторная аутентификация включена");
    });

    // An unnamed refusal goes under the code: under the password it would send the reader to retype a right one.
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

    /** A refusal goes under the code field; an expired session and a dead connection keep the plate. */
    it.each([
      [500, ["Ошибка сервера"]],
      [401, []],
    ])("answers a %i on the pair", async (status, lines) => {
      const wrapper = await disabling();

      vi.spyOn(accountApi, "disableTwoFactor").mockResolvedValue({
        data: null,
        error: { status, title: status ? "Ошибка сервера" : "" },
      } as never);

      await wrapper.find(".confirm-form").trigger("submit");
      await flushPromises();

      expect(
        wrapper.findAll(".form-field-error").map((node) => node.text()),
      ).toEqual(lines);
      // The factor is still on and the pair still open.
      expect(wrapper.text()).toContain("Двухфакторная аутентификация включена");
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

      expect(wrapper.text()).toContain(
        "Двухфакторная аутентификация выключена",
      );
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
      expect(wrapper.text()).toContain("Новый набор заменил прежний");
      expect(wrapper.findAll(".code-item")).toHaveLength(10);
    });
  });
});

// A date is never broken: the line breaks between the label and the value.
describe("what the factor knows about itself", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  it("is one line of text a row: the label, a space, the value", async () => {
    const wrapper = await withStatus({}, ON);

    const rows = wrapper.findAll(".status-row").map((row) => row.text());
    expect(rows).toHaveLength(3);
    expect(rows[0]).toMatch(/^Включена: \d{2}\.\d{2}\.\d{4}\sв\s\d{2}:\d{2}$/);
    expect(rows[2]).toBe("Резервных кодов осталось: 7");
  });
});
