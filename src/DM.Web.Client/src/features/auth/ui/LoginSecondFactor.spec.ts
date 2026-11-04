/**
 * @vitest-environment jsdom
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { completeSecondFactor } from "@/entities/user";
import { atSecondFactor, resetLoginForm, typeCode } from "./LoginForm.fixtures";
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

// A browser cuts a paste to maxlength before any event and jsdom does not, so the cut is done here.
async function pasteCode(
  wrapper: Awaited<ReturnType<typeof atSecondFactor>>,
  text: string,
) {
  const field = wrapper.find("#two-factor-code");
  const limit = field.attributes("maxlength");

  await field.setValue(limit ? text.slice(0, Number(limit)) : text);
  await flushPromises();
}

// By caption, not position: a swap of the two would otherwise pass.
async function chooseCodeKind(
  wrapper: Awaited<ReturnType<typeof atSecondFactor>>,
  label: string,
) {
  const link = wrapper
    .findAll(".auth-label-row .auth-action")
    .find(
      (option) =>
        option.text() === `Ввести ${label[0].toLowerCase()}${label.slice(1)}`,
    );

  if (!link) throw new Error(`нет ссылки на "${label}"`);
  await link.trigger("click");
  await flushPromises();
}

describe("LoginForm", () => {
  beforeEach(resetLoginForm);

  describe("second factor", () => {
    it("asks for the code instead of reporting a session", async () => {
      const wrapper = await atSecondFactor();

      expect(wrapper.emitted("success")).toBeUndefined();
      expect(wrapper.text()).toContain("Подтверждение входа");
      expect(wrapper.find("#two-factor-code").exists()).toBe(true);
      expect(wrapper.find("#password").exists()).toBe(false);
    });

    it("puts the cursor in the field it just asked for", async () => {
      const wrapper = await atSecondFactor();

      expect(document.activeElement).toBe(
        wrapper.find("#two-factor-code").element,
      );
    });

    it("keeps the button shut until the code is whole", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(null);
      const wrapper = await atSecondFactor();

      expect(wrapper.find(".auth-button").attributes("disabled")).toBeDefined();

      await typeCode(wrapper, "12345");

      expect(completeSecondFactor).not.toHaveBeenCalled();
      expect(wrapper.find(".auth-button").attributes("disabled")).toBeDefined();
    });

    it("checks a whole code without being asked", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(null);
      const wrapper = await atSecondFactor();

      await typeCode(wrapper, "123456");

      expect(completeSecondFactor).toHaveBeenCalledWith("123456");
      expect(wrapper.emitted("success")).toHaveLength(1);
    });

    it("takes the button out while the check is running, and keeps its caption", async () => {
      let answer: (failure: null) => void = () => {};
      vi.mocked(completeSecondFactor).mockReturnValue(
        new Promise((resolve) => {
          answer = resolve;
        }),
      );
      const wrapper = await atSecondFactor();

      await typeCode(wrapper, "123456");

      const button = wrapper.find(".auth-button");
      expect(button.text()).toBe("Подтвердить");
      expect(button.attributes("disabled")).toBeDefined();
      expect(button.attributes("aria-busy")).toBe("true");

      answer(null);
      await flushPromises();

      expect(wrapper.emitted("success")).toHaveLength(1);
    });

    it("keeps the whole code out of a paste longer than the field", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(null);
      const wrapper = await atSecondFactor();

      await pasteCode(wrapper, "код из письма: 123456");

      expect(
        (wrapper.find("#two-factor-code").element as HTMLInputElement).value,
      ).toBe("123456");
    });

    it("does not check by itself a recovery code pasted into the digit field", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(null);
      const wrapper = await atSecondFactor();

      await pasteCode(wrapper, "7K3M-9Q2W-XZ8P-4R6T");

      expect(
        (wrapper.find("#two-factor-code").element as HTMLInputElement).value,
      ).toBe("739284");
      expect(completeSecondFactor).not.toHaveBeenCalled();
      expect(
        wrapper.find(".auth-button").attributes("disabled"),
      ).toBeUndefined();

      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(completeSecondFactor).toHaveBeenCalledWith("739284");
    });

    it("checks by itself a code pasted with the dashes it draws", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(null);
      const wrapper = await atSecondFactor();

      await chooseCodeKind(wrapper, "Резервный код");
      await typeCode(wrapper, "7K3M-9Q2W-XZ8P-4R6T");

      expect(completeSecondFactor).toHaveBeenCalledWith("7K3M9Q2WXZ8P4R6T");
    });

    it("checks again when the form is submitted after a refusal", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(
        problem(400, "Неверный код"),
      );
      const wrapper = await atSecondFactor();

      await typeCode(wrapper, "123456");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(completeSecondFactor).toHaveBeenCalledTimes(2);
    });

    it("does not start a second check over the first", async () => {
      vi.mocked(completeSecondFactor).mockReturnValue(new Promise(() => {}));
      const wrapper = await atSecondFactor();

      await typeCode(wrapper, "123456");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(completeSecondFactor).toHaveBeenCalledTimes(1);
    });

    it("says the code is wrong, in the words of this screen", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(
        problem(400, "Неверный код"),
      );

      const wrapper = await atSecondFactor();
      await typeCode(wrapper, "123456");

      expect(wrapper.emitted("success")).toBeUndefined();
      const message = wrapper.find("#two-factor-code-message");
      expect(message.text()).toBe("Неверный код");
      expect(message.classes()).toContain("auth-message--error");
    });

    it("selects a code that did not pass", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(
        problem(400, "Неверный код"),
      );

      const wrapper = await atSecondFactor();
      await typeCode(wrapper, "123456");

      const field = wrapper.find("#two-factor-code")
        .element as HTMLInputElement;
      expect(document.activeElement).toBe(field);
      expect([field.selectionStart, field.selectionEnd]).toEqual([0, 6]);
    });

    it("drops the refusal as soon as the code is edited", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(
        problem(400, "Неверный код"),
      );

      const wrapper = await atSecondFactor();
      await typeCode(wrapper, "123456");
      expect(wrapper.find("#two-factor-code-message").text()).toBe(
        "Неверный код",
      );

      await typeCode(wrapper, "12345");

      expect(wrapper.find("#two-factor-code-message").text()).toBe(
        "Шесть цифр из приложения-аутентификатора",
      );
    });

    // The composable writes the field by hand here, which fires no input event.
    it("drops the refusal when Backspace deletes through a dash", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(
        problem(400, "Неверный код"),
      );

      const wrapper = await atSecondFactor();
      await chooseCodeKind(wrapper, "Резервный код");
      await typeCode(wrapper, "7K3M9Q2WXZ8P4R6T");
      expect(wrapper.find("#two-factor-code-message").text()).toBe(
        "Неверный код",
      );

      // The caret right after the first dash.
      const field = wrapper.find("#two-factor-code")
        .element as HTMLInputElement;
      field.setSelectionRange(5, 5);
      await wrapper
        .find("#two-factor-code")
        .trigger("keydown", { key: "Backspace" });
      await flushPromises();

      expect(field.value).toBe("7K39-Q2WX-Z8P4-R6T");
      expect(wrapper.find("#two-factor-code-message").text()).toBe(
        "Шестнадцать знаков из списка резервных кодов",
      );
    });

    it("remakes the field for the kind of code the link chooses", async () => {
      const wrapper = await atSecondFactor();
      const field = () => wrapper.find("#two-factor-code");
      const attribute = (name: string) => field().attributes(name);

      expect(attribute("inputmode")).toBe("numeric");
      expect(attribute("autocapitalize")).toBe("off");
      expect(field().classes()).not.toContain("auth-input--code-recovery");

      await chooseCodeKind(wrapper, "Резервный код");

      expect(attribute("inputmode")).toBe("text");
      expect(attribute("autocapitalize")).toBe("characters");
      expect(field().classes()).toContain("auth-input--code-recovery");

      await chooseCodeKind(wrapper, "Код из приложения");

      expect(attribute("inputmode")).toBe("numeric");
    });

    it("names the kind in the label and offers the other one beside it", async () => {
      const wrapper = await atSecondFactor();
      const label = () => wrapper.find('label[for="two-factor-code"]').text();
      const link = () => wrapper.find(".auth-label-row .auth-action").text();

      expect(label()).toBe("Код из приложения");
      expect(link()).toBe("Ввести резервный код");

      await chooseCodeKind(wrapper, "Резервный код");

      expect(label()).toBe("Резервный код");
      expect(link()).toBe("Ввести код из приложения");
    });

    it("empties the field when the kind of code changes", async () => {
      const wrapper = await atSecondFactor();

      await typeCode(wrapper, "12345");
      await chooseCodeKind(wrapper, "Резервный код");

      expect(
        (wrapper.find("#two-factor-code").element as HTMLInputElement).value,
      ).toBe("");
      expect(document.activeElement).toBe(
        wrapper.find("#two-factor-code").element,
      );
    });

    it("offers one switch, beside the label that names the kind", async () => {
      const wrapper = await atSecondFactor();

      expect(wrapper.findAll(".auth-label-row .auth-action")).toHaveLength(1);
      expect(wrapper.text()).toContain("Ввести резервный код");
      expect(wrapper.text()).not.toContain("Ввести код из приложения");
    });

    it("sends a recovery code without the dashes it shows", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(null);
      const wrapper = await atSecondFactor();

      await chooseCodeKind(wrapper, "Резервный код");
      await typeCode(wrapper, "7K3M9Q2WXZ8P4R6T");

      expect(
        (wrapper.find("#two-factor-code").element as HTMLInputElement).value,
      ).toBe("7K3M-9Q2W-XZ8P-4R6T");
      expect(completeSecondFactor).toHaveBeenCalledWith("7K3M9Q2WXZ8P4R6T");
    });

    it("hands over the reader who has neither the phone nor the codes", async () => {
      const wrapper = await atSecondFactor();
      const footnote = wrapper.find(".auth-footnote");

      expect(footnote.text()).toContain(
        "Нет доступа к приложению и резервным кодам?",
      );

      const away = footnote.find("button");
      expect(away.text()).toBe("Отключить двухфакторную аутентификацию");

      await away.trigger("click");

      expect(wrapper.emitted("cantPassSecondFactor")).toHaveLength(1);
    });
  });
});
