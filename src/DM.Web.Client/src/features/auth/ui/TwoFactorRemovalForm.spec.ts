/**
 * @vitest-environment jsdom
 */
import { afterEach, describe, expect, it, vi, beforeEach } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { accountApi } from "@/entities/user";
import { Api } from "@/shared/api";
import TwoFactorRemovalForm from "./TwoFactorRemovalForm.vue";
import { toast } from "./authForms.doubles";
import {
  address,
  AuthDialogStub,
  expectWaitDropsOnClose,
  expectWaitRunsDown,
  mountAuthForm,
} from "./authForms.fixtures";

vi.mock("@/shared/lib/composables/useToast", async () =>
  (await import("./authForms.doubles")).toastComposable(),
);

enableAutoUnmount(afterEach);

// Attached to the document, because focus is only observable there.
const mountForm = () =>
  mountAuthForm(TwoFactorRemovalForm, {
    attachTo: document.body,
    stubs: { AuthDialog: AuthDialogStub },
  });

async function submit(email = "reader@dm.am", mount = mountForm) {
  const wrapper = await mount();

  await wrapper.find("#two-factor-removal-email").setValue(email);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
  return wrapper;
}

describe("the mailed removal request", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("asks for the address", async () => {
    const wrapper = await mountForm();

    expect(wrapper.get(".auth-title").text()).toBe("Запрос на отключение");
    expect(wrapper.text()).not.toContain("Нет доступа к приложению?");
    expect(
      wrapper.text(),
      'у термина одно слово: "двухфакторная аутентификация", не "защита"',
    ).not.toContain("защит");
    expect(wrapper.find("#two-factor-removal-email").exists()).toBe(true);
  });

  it("puts the delay in a centred warning box", async () => {
    const box = (await mountForm()).get(".auth-warnbox");
    const lead = box.get("b").text();

    expect(box.classes()).toContain("auth-warnbox--notice");
    expect([lead, box.text().slice(lead.length)]).toEqual([
      "Отключение выполняется с задержкой",
      "Ссылка из письма назначит отключение двухфакторной аутентификации через 7 дней",
    ]);
  });

  it("offers one control, and it names the result", async () => {
    const wrapper = await mountForm();

    const captions = wrapper.findAll("button").map((b) => b.text());
    expect(captions).toEqual(["Отправить письмо", "поддержку"]);
  });

  describe("no access to the mailbox either", () => {
    it("closes the dialog and opens the support form", async () => {
      const wrapper = await mountForm();

      expect(wrapper.text()).toContain("Нужна помощь? Обратитесь в");
      await wrapper
        .findAll("button")
        .find((b) => b.text() === "поддержку")
        ?.trigger("click");
      await flushPromises();

      expect(
        wrapper.findComponent(TwoFactorRemovalForm).emitted("cancel"),
      ).toHaveLength(1);
      expect(address()).toBe("/support?subtype=AccessRecovery");
    });

    it("leaves the receipt without the offer", async () => {
      vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
        data: null,
        error: null,
      });

      const wrapper = await submit();

      expect(wrapper.text()).toContain("Проверьте почту");
      expect(wrapper.text()).not.toContain("Нужна помощь");
    });
  });

  it("puts the caret in the field it opens with", async () => {
    const wrapper = await mountForm();
    await flushPromises();

    expect(document.activeElement).toBe(
      wrapper.find("#two-factor-removal-email").element,
    );
  });

  it("opens the button on anything typed and names the format on the press", async () => {
    const send = vi
      .spyOn(accountApi, "requestTwoFactorRemoval")
      .mockResolvedValue({ data: null, error: null } as never);
    const wrapper = await mountForm();
    const button = () => wrapper.find("button[type='submit']");

    expect(button().attributes("disabled")).toBeDefined();

    await wrapper.find("#two-factor-removal-email").setValue("reader@");
    expect(button().attributes("disabled")).toBeUndefined();

    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(send).not.toHaveBeenCalled();
    expect(wrapper.text()).not.toContain("Проверьте почту");
    const strip = wrapper.find(".auth-message");
    expect(strip.text()).toBe("Неверный формат почты");
    expect(strip.classes()).toContain("auth-message--error");
  });

  it("keeps the strip under the field with nothing in it", async () => {
    const wrapper = await mountForm();

    const strip = wrapper.find(".auth-message");
    expect(strip.exists()).toBe(true);
    expect(strip.text()).toBe("");
    expect(strip.classes()).not.toContain("auth-message--error");
  });

  it("says under the field that the letter did not go out", async () => {
    vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: { status: 500, title: "Ошибка сервера" },
    } as never);

    const wrapper = await submit();

    expect(wrapper.text()).not.toContain("Проверьте почту");
    const strip = wrapper.find(".auth-message");
    expect(strip.text()).toBe("Ошибка сервера");
    expect(strip.classes()).toContain("auth-message--error");
  });

  it("says the same thing when nothing came back at all", async () => {
    vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: { status: 0, title: "" },
    } as never);

    const wrapper = await submit();

    expect(wrapper.text()).not.toContain("Проверьте почту");
    expect(wrapper.find(".auth-message").text()).toBe("Ошибка сервера");
    expect(wrapper.text()).not.toContain("Попробуйте позже");
  });

  it("keeps the form up when the letter did not go out", async () => {
    vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: { status: 400, title: "Неверный формат почты" },
    } as never);

    const wrapper = await submit();

    expect(wrapper.text()).not.toContain("Проверьте почту");
    const strip = wrapper.find(".auth-message");
    expect(strip.text()).toBe("Неверный формат почты");
    expect(strip.classes()).toContain("auth-message--error");
  });

  // Followed through the real service to the client: only the press sets the flags.
  it("presses with the refusal and the limit taken over from the interceptor", async () => {
    const post = vi
      .spyOn(Api, "post")
      .mockResolvedValue({ data: null, error: null } as never);

    await submit("nobody@dm.am");

    expect(post).toHaveBeenCalledWith(
      "account/two-factor/removal",
      { email: "nobody@dm.am" },
      expect.objectContaining({ ownsRefusal: true, ownsRateLimit: true }),
    );
  });

  describe("a rate limit", () => {
    const limited = (retryAfterSeconds?: number) =>
      vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
        data: null,
        error: {
          status: 429,
          title: "Слишком много запросов",
          retryAfterSeconds,
        },
      } as never);

    beforeEach(() => {
      // Only the interval: everything else in this file runs on real time.
      vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    // Off the document: jsdom keeps the focus of an attached page with a
    // setInterval of its own, which the faked interval would count as a wait.
    const mountOffDocument = () =>
      mountAuthForm(TwoFactorRemovalForm, {
        stubs: { AuthDialog: AuthDialogStub },
      });

    // First in the block: an attached form here fails every time, not one run in many.
    it("drops the wait when the dialog closes", async () => {
      limited(30);
      const wrapper = await submit(undefined, mountOffDocument);
      await expectWaitDropsOnClose(wrapper, ".auth-message");
    });

    it("stands under the field, shuts the button, and runs down", async () => {
      limited(3);
      const wrapper = await submit(undefined, mountOffDocument);

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#two-factor-removal-email").exists()).toBe(true);
      expect(toast.warning).not.toHaveBeenCalled();
      await expectWaitRunsDown(wrapper, ".auth-message");
    });

    it("drops the wait when the card leaves the page", async () => {
      limited(30);
      const wrapper = await submit(undefined, mountOffDocument);
      expect(vi.getTimerCount()).toBe(1);

      wrapper.unmount();

      expect(vi.getTimerCount()).toBe(0);
    });
  });

  // One screen for every address: whether an account exists is not disclosed.
  describe("the receipt", () => {
    // Not the default of the setting: the receipt says the hours the answer named.
    beforeEach(() => {
      vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
        data: { resource: { linkLifetimeHours: 22 } },
        error: null,
      } as never);
    });

    it("names the letter and the address, and discloses nothing else", async () => {
      const wrapper = await submit("nobody@dm.am");

      expect(wrapper.text()).toContain("Проверьте почту");
      expect(wrapper.text()).toContain(
        "Мы отправили письмо со ссылкой для отключения двухфакторной аутентификации на",
      );
      expect(wrapper.text()).toContain("nobody@dm.am");
      expect(
        wrapper.text(),
        'у термина одно слово: "двухфакторная аутентификация", не "защита"',
      ).not.toContain("защит");
      expect(wrapper.text()).not.toContain("Если аккаунт есть");
      expect(wrapper.text()).not.toContain("не найден");
      expect(wrapper.find("#two-factor-removal-email").exists()).toBe(false);
    });

    it("names the life of the link the answer gave and not the waiting period", async () => {
      const wrapper = await submit();

      expect(wrapper.text()).toContain("Ссылка действительна 22 часа");
      expect(wrapper.text()).not.toContain("7 дней");
    });

    it("carries no button", async () => {
      const wrapper = await submit();

      expect(wrapper.findAll("button")).toHaveLength(0);
    });
  });
});
