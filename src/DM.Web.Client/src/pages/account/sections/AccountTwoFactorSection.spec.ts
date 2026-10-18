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
 * "уже включена", and the reply would land under a password field where there
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
 *
 * And the fourth, added when the plates went: the clipboard is the one result
 * the screen cannot show, so the control that was pressed says what happened
 * to it. Both copies here - the secret and the whole set of codes - answer in
 * their own caption and raise nothing in the corner.
 *
 * The fifth is who speaks about a failed read. The card stands in place of the
 * block, so the read claims the refusal and the limit and the card prints what
 * the server named - a rank this block is closed for is worth more to the
 * reader than "не удалось узнать", and nothing repeats it in a plate above.
 * Two statuses are the exception, and they are the two the interceptor knows
 * more about than this section: there the card names what is missing and the
 * plate says why.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import { COPIED, COPY_FAILED } from "@/shared/lib/composables/useCopyFeedback";
import AccountTwoFactorSection from "./AccountTwoFactorSection.vue";
import type { TwoFactorStatus } from "@/shared/api/models/account";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  compileSass,
  sassBlocks,
  styleRules,
} from "@/shared/lib/testing/styles";
import { readFileSync } from "node:fs";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((one) => one.message);

/** Stands in for the clipboard, which jsdom has none of. */
let write: ReturnType<typeof vi.fn>;

const options = () => ({
  global: { plugins: [createPinia()] },
});

const OFF: TwoFactorStatus = {
  enabled: false,
  recoveryCodesLeft: 0,
};

const ON: TwoFactorStatus = {
  enabled: true,
  enabledUtc: "2026-05-01T10:00:00Z",
  lastVerifiedUtc: "2026-08-20T18:30:00Z",
  recoveryCodesLeft: 7,
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
    write = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText: write } });
    [...toast.toasts.value].forEach((one) => toast.dismiss(one.id));
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

      expect(wrapper.find(".status-card--failed").exists()).toBe(true);
      expect(wrapper.text()).not.toContain(
        "Двухфакторная аутентификация выключена",
      );
    });

    /** Mounts the section over a read the server refused. */
    async function failedRead(error: Record<string, unknown>) {
      vi.spyOn(accountApi, "getTwoFactorStatus").mockResolvedValue({
        data: null,
        error,
      } as never);

      const wrapper = mount(AccountTwoFactorSection, options());
      await flushPromises();
      return wrapper.get(".status-card--failed");
    }

    /**
     * The card stands instead of the whole block, which makes it the one
     * carrier of a failed read - and the read says so, or the interceptor
     * raises a plate over the card with the same sentence in it one line
     * higher. Asserted at the call and not at the transport: the request is
     * mocked out here, and a flag left behind is invisible everywhere else in
     * this file.
     */
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

    /**
     * And having taken it over, the card says what the server said. The section
     * used to print its own line instead, on the grounds that the plate carried
     * the server's - so a rank this block is closed for arrived as "не удалось
     * узнать", which names nothing the reader can act on, and the sentence that
     * did name it stood in a plate that fades.
     */
    it.each([403, 500])(
      "prints what the server said about a %i",
      async (status) => {
        const card = await failedRead({ status, title: "Отключено для роли" });

        expect(card.text()).toContain("Отключено для роли");
        expect(card.text()).not.toContain("Не удалось узнать состояние");
        // The way on is the section's own and stays whatever the server said.
        expect(card.text()).toContain("Обновите страницу");
      },
    );

    /**
     * The wait is built and not relayed: the seconds the server named ride in
     * Retry-After, which the transport copies onto the failure, and the title
     * of a 429 carries no number at all. Printed as it arrives, the card would
     * shut the block with "Слишком много запросов" and leave the reader to
     * guess for how long.
     */
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

    /**
     * The two the interceptor still speaks for, and knows more about than this
     * block does: a session that ended is about the session and not about the
     * factor, and a request that never went out has no sentence to relay. The
     * card names what is missing, the plate says why.
     */
    it.each([0, 401])("keeps its own line for a %i", async (status) => {
      const card = await failedRead({ status, title: "Unauthorized" });

      expect(card.text()).toContain(
        "Не удалось узнать состояние двухфакторной аутентификации",
      );
      expect(card.text()).not.toContain("Unauthorized");
    });

    // Nobody else speaks for this one, so the card relays what the server said.
    it("relays a refusal the interceptor keeps quiet about", async () => {
      const card = await failedRead({
        status: 400,
        title: "Состояние недоступно",
      });

      expect(card.text()).toContain("Состояние недоступно");
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

    /**
     * The statuses the interceptor used to answer with a plate of its own are
     * said under the field now, and the plate is taken down (the owner's rule
     * of 26 September 2026): the field is still on screen with the password
     * in it, and that is where the reader looks.
     */
    it.each([
      [403, "Ошибка сервера"],
      [429, "Слишком много попыток, попробуйте позже"],
      [500, "Ошибка сервера"],
    ])("says a %i on the password under the field", async (status, line) => {
      const wrapper = await withStatus({});
      await wrapper.find("button").trigger("click");

      vi.spyOn(accountApi, "setupTwoFactor").mockResolvedValue({
        data: null,
        error: { status, title: "Ошибка сервера" },
      } as never);

      await wrapper.find("#two-factor-password").setValue("correct horse");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(wrapper.find(".form-field-error").text()).toBe(line);
      // Refused all the same: no secret was issued, so the code field is shut.
      expect(wrapper.find("#two-factor-code").exists()).toBe(false);
    });

    // An expired session and a dead connection are about the reader, not the
    // press: the plate says them, and the field keeps quiet.
    it.each([0, 401])(
      "leaves a %i on the password to the plate",
      async (status) => {
        const wrapper = await withStatus({});
        await wrapper.find("button").trigger("click");

        vi.spyOn(accountApi, "setupTwoFactor").mockResolvedValue({
          data: null,
          error: { status, title: "" },
        } as never);

        await wrapper.find("#two-factor-password").setValue("correct horse");
        await wrapper.find("form").trigger("submit");
        await flushPromises();

        expect(wrapper.find(".form-field-error").exists()).toBe(false);
        expect(wrapper.find("#two-factor-code").exists()).toBe(false);
      },
    );

    // Without a camera the square is useless, and the string beside it is the
    // whole of what such a reader has.
    it("shows the secret as text next to the square", async () => {
      const wrapper = await issued();

      expect(wrapper.text()).toContain("JBSWY3DPEHPK3PXP");
      expect(wrapper.text()).toContain("Или введите секрет вручную");
      expect(wrapper.find("#two-factor-code").exists()).toBe(true);
    });

    // Nothing on screen changes when a secret goes into the clipboard, so the
    // button that was pressed is the only place that can say it did.
    it("says the secret is copied in the button that copied it", async () => {
      const wrapper = await issued();

      const copy = wrapper.find(".copy-btn");
      await copy.trigger("click");
      await flushPromises();

      expect(write).toHaveBeenCalledWith("JBSWY3DPEHPK3PXP");
      expect(copy.text()).toBe(COPIED);
      expect(saidOutLoud()).toEqual([]);
    });

    // The half of it a reader must not miss: told nothing, they go and paste
    // whatever was in the clipboard before.
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

    // Same rule one field further on: the code field stays on screen, and the
    // line under it says what the plate used to.
    it.each([
      [403, "Ошибка сервера"],
      [429, "Слишком много попыток, попробуйте позже"],
      [500, "Ошибка сервера"],
    ])("says a %i on the code under the field", async (status, line) => {
      const wrapper = await issued();

      vi.spyOn(accountApi, "confirmTwoFactor").mockResolvedValue({
        data: null,
        error: { status, title: "Ошибка сервера" },
      } as never);

      await wrapper.find("#two-factor-code").setValue("000000");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(wrapper.find(".form-field-error").text()).toBe(line);
      // The factor is not on: no codes were handed over.
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
      expect(wrapper.text()).toContain("Двухфакторная аутентификация включена");
    });

    // The set exists in one showing, and what the reader saves has to be the
    // set they saw: the same ten codes, grouped by four, one to a line.
    it("puts the whole set in the clipboard as the screen reads it", async () => {
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
      expect(wrapper.text()).toContain("Двухфакторная аутентификация включена");
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

    /**
     * The statuses the interceptor used to plate are said under the code, the
     * field an unnamed sentence goes to, and the plate is taken down. Neither
     * field for an expired session or a dead connection, which keep the plate.
     */
    it.each([
      [403, ["Ошибка сервера"]],
      [429, ["Слишком много попыток, попробуйте позже"]],
      [500, ["Ошибка сервера"]],
      [401, []],
      [0, []],
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
      // Refused all the same: the factor is still on and the pair is still open.
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

// A state is written in words on this site and not drawn as a panel: the
// pills of the account page went for that reason, and these cards wore the
// same thing at the size of a block, a wash of colour in a frame of it with a
// mark beside the words. What is left of a card is the sentence in the colour
// of what it means.
describe("the state of the second factor", () => {
  const file = join(
    dirname(fileURLToPath(import.meta.url)),
    "AccountTwoFactorSection.vue",
  );
  const rules = () =>
    styleRules(compileSass(sassBlocks(file), file)).filter(({ selector }) =>
      /status-(card|header|title|icon)/.test(selector),
    );

  it("is not a panel: no fill and no frame", () => {
    const panels = rules().filter(({ declarations }) =>
      Object.keys(declarations).some((name) =>
        /^(background|border)/.test(name),
      ),
    );
    expect(panels).toEqual([]);
  });

  it("has no mark beside its words", () => {
    expect(
      rules().filter(({ selector }) => /status-icon/.test(selector)),
    ).toEqual([]);
    expect(readFileSync(file, "utf8")).not.toContain("status-icon");
  });
});

// On a phone "Последнее подтверждение:" and its date do not fit one line, and
// the date broke after "в" with the time under it. A date is never broken: the
// line breaks between the label and the value, and the value goes down whole.
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

  it("never breaks a value in two", () => {
    const file = join(
      dirname(fileURLToPath(import.meta.url)),
      "AccountTwoFactorSection.vue",
    );
    const rules = styleRules(compileSass(sassBlocks(file), file));
    const value = rules.find(({ selector }) => selector === ".status-value");
    expect(value?.declarations["white-space"]).toBe("nowrap");
    const row = rules.find(({ selector }) => selector === ".status-row");
    expect(row?.declarations.display).toBeUndefined();
  });
});

// The two ways out stand in one line beside each other, as on the full
// version: a phone shows the same row, with nothing put into "…" and no
// column of buttons of its own.
describe("the two ways out of the factor", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  it("stand side by side in a plain row, neither held back", async () => {
    const wrapper = await withStatus({}, ON);

    const row = wrapper.get(".two-factor-actions");
    expect(row.classes()).not.toContain("button-row");
    expect(row.attributes()).not.toHaveProperty("data-fit-row");
    const actions = Array.from(row.element.children).map((child) => [
      (child.textContent ?? "").trim(),
      child.hasAttribute("data-fit-keep"),
    ]);
    expect(actions).toEqual([
      ["Перевыпустить коды", false],
      ["Отключить", false],
    ]);
  });

  // jsdom lays nothing out, so the width rules are read off the stylesheet:
  // the block has no form of its own for a narrow screen, and the codes stand
  // in two columns there as they do on the full version.
  it("has no form of its own for a phone", () => {
    const file = join(
      dirname(fileURLToPath(import.meta.url)),
      "AccountTwoFactorSection.vue",
    );
    const compiled = compileSass(sassBlocks(file), file);
    expect(compiled).not.toContain("@media");
    expect(compiled).not.toContain("@container");
    const rules = styleRules(compiled);
    const grid = rules.find(({ selector }) => selector === ".codes-grid");
    expect(grid?.declarations["grid-template-columns"]).toBe(
      "repeat(2, minmax(0, 1fr))",
    );
  });
});
