/**
 * @vitest-environment jsdom
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { nextTick } from "vue";
import { accountApi } from "@/entities/user";
import { Api } from "@/shared/api";
import AccessRecoveryForm from "./AccessRecoveryForm.vue";
import { toast } from "./authForms.doubles";
import {
  address,
  AuthDialogStub,
  expectWaitRunsDown,
  mountAuthForm,
} from "./authForms.fixtures";

vi.mock("@/shared/lib/composables/useToast", async () =>
  (await import("./authForms.doubles")).toastComposable(),
);

enableAutoUnmount(afterEach);

// Attached to the document, because focus is only observable there.
const mountForm = (prefillEmail?: string) =>
  mountAuthForm(AccessRecoveryForm, {
    props: prefillEmail === undefined ? {} : { prefillEmail },
    attachTo: document.body,
    stubs: { AuthDialog: AuthDialogStub },
  });

type Wrapper = Awaited<ReturnType<typeof mountForm>>;

const control = (wrapper: Wrapper, caption: string) =>
  wrapper.findAll("button").find((button) => button.text() === caption);

const answered = (status: string, linkLifetimeHours = 48) =>
  vi.spyOn(accountApi, "recover").mockResolvedValue({
    data: { status, email: "reader@dm.am", linkLifetimeHours },
    error: null,
  } as never);

const refused = (status: number, title: string, retryAfterSeconds?: number) =>
  vi.spyOn(accountApi, "recover").mockResolvedValue({
    data: null,
    error: { status, title, retryAfterSeconds },
  } as never);

async function submit(wrapper: Wrapper, email = "reader@dm.am") {
  await wrapper.find("#recovery-email").setValue(email);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
  return wrapper;
}

describe("the recovery request", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("asks for one address and names what the button will do", async () => {
    const wrapper = await mountForm();

    expect(wrapper.text()).toContain("Восстановление доступа");
    expect(wrapper.find("#recovery-email").exists()).toBe(true);
    expect(control(wrapper, "Отправить письмо")).toBeDefined();
  });

  it("opens the button on anything typed and names the format on the press", async () => {
    const send = answered("PasswordResetSent");
    const wrapper = await mountForm();
    const button = () => wrapper.find("button[type='submit']");

    expect(button().attributes("disabled")).toBeDefined();

    await wrapper.find("#recovery-email").setValue("reader@");
    expect(button().attributes("disabled")).toBeUndefined();

    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(send).not.toHaveBeenCalled();
    expect(wrapper.text()).not.toContain("Проверьте почту");
    const strip = wrapper.find(".auth-message");
    expect(strip.text()).toBe("Неверный формат почты");
    expect(strip.classes()).toContain("auth-message--error");
  });

  describe("the open dialog", () => {
    it("puts the caret in the field", async () => {
      const wrapper = await mountForm();
      await flushPromises();

      expect(document.activeElement).toBe(
        wrapper.find("#recovery-email").element,
      );
    });

    it("keeps the caret there when the address was handed over", async () => {
      const wrapper = await mountForm("lex@dm.am");
      await flushPromises();

      const field = wrapper.find("#recovery-email").element as HTMLInputElement;
      expect(field.value).toBe("lex@dm.am");
      expect(document.activeElement).toBe(field);
    });
  });

  describe("no access to the mailbox either", () => {
    it("closes the dialog and opens support on the recovery category", async () => {
      const wrapper = await mountForm();

      await control(wrapper, "Нет доступа к почте?")?.trigger("click");
      await flushPromises();

      expect(
        wrapper.findComponent(AccessRecoveryForm).emitted("cancel"),
      ).toHaveLength(1);
      expect(address()).toBe("/support?subtype=AccessRecovery");
    });
  });

  describe("the three answers", () => {
    it("names the reset letter it sent", async () => {
      answered("PasswordResetSent");
      const wrapper = await submit(await mountForm());

      expect(wrapper.text()).toContain("Проверьте почту");
      expect(wrapper.text()).toContain("со ссылкой для сброса пароля");
    });

    it("names the activation letter instead when that is what went out", async () => {
      answered("ActivationResent");
      const wrapper = await submit(await mountForm());

      expect(wrapper.text()).toContain("со ссылкой для активации");
    });

    // Hours other than the default: the receipt says what the answer named.
    it("gives each letter the lifetime its answer names", async () => {
      answered("PasswordResetSent", 21);
      const reset = await submit(await mountForm());
      expect(reset.text()).toContain("Ссылка действительна 21 час");

      answered("ActivationResent", 30);
      const activation = await submit(await mountForm());
      expect(activation.text()).toContain("Ссылка действительна 30 часов");
    });

    it("leaves no button on either receipt", async () => {
      answered("PasswordResetSent");
      const reset = await submit(await mountForm());
      expect(reset.findAll("button")).toHaveLength(0);

      answered("ActivationResent");
      const activation = await submit(await mountForm());
      expect(activation.findAll("button")).toHaveLength(0);
    });

    it("says there is no such account and offers the field back", async () => {
      answered("NotFound");
      const wrapper = await submit(await mountForm());

      expect(wrapper.text()).toContain("Аккаунт не найден");

      await control(wrapper, "Попробовать другую почту")?.trigger("click");
      expect(wrapper.find("#recovery-email").exists()).toBe(true);
    });
  });

  describe("a refusal", () => {
    describe("a rate limit", () => {
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
        mountAuthForm(AccessRecoveryForm, {
          stubs: { AuthDialog: AuthDialogStub },
        });

      // The modal keeps the component mounted, so a running interval would outlive it.
      // First in the block: an attached form here fails every time, not one run in many.
      it("drops the wait when the dialog closes", async () => {
        refused(429, "Слишком много запросов", 30);
        const wrapper = await submit(await mountOffDocument());
        // A real frame of jsdom runs out before the close, the order a loaded runner met.
        await new Promise((resolve) => setTimeout(resolve, 50));
        expect(vi.getTimerCount()).toBe(1);

        wrapper.findComponent({ name: "AuthDialog" }).vm.$emit("beforeClose");
        await nextTick();

        expect(vi.getTimerCount()).toBe(0);
        expect(wrapper.find(".auth-message").text()).toBe("");
      });

      it("stands under the field, shuts the button, and runs down", async () => {
        refused(429, "Слишком много запросов", 3);
        const wrapper = await submit(await mountOffDocument());

        expect(wrapper.text()).not.toContain("Проверьте почту");
        expect(toast.warning).not.toHaveBeenCalled();
        await expectWaitRunsDown(wrapper, ".auth-message");
      });

      it("drops the wait when the card leaves the page", async () => {
        refused(429, "Слишком много запросов", 30);
        const wrapper = await submit(await mountOffDocument());
        expect(vi.getTimerCount()).toBe(1);

        wrapper.unmount();

        expect(vi.getTimerCount()).toBe(0);
      });
    });

    it("says under the field what the server said", async () => {
      refused(400, "Неверный формат почты");
      const wrapper = await submit(await mountForm());

      expect(wrapper.text()).not.toContain("Проверьте почту");
      const strip = wrapper.find(".auth-message");
      expect(strip.text()).toBe("Неверный формат почты");
      expect(strip.classes()).toContain("auth-message--error");
    });

    it("says under the field that the letter did not go out", async () => {
      refused(500, "Ошибка сервера");
      const wrapper = await submit(await mountForm());

      expect(wrapper.text()).not.toContain("Проверьте почту");
      const strip = wrapper.find(".auth-message");
      expect(strip.text()).toBe("Ошибка сервера");
      expect(strip.classes()).toContain("auth-message--error");
    });

    it("says the same thing when nothing readable came back", async () => {
      refused(0, "");
      const wrapper = await submit(await mountForm());

      expect(wrapper.find(".auth-message").text()).toBe("Ошибка сервера");
      expect(wrapper.text()).not.toContain("Попробуйте позже");
    });

    it("says the same thing when the answer carried no payload", async () => {
      vi.spyOn(accountApi, "recover").mockResolvedValue({
        data: null,
        error: null,
      } as never);
      const wrapper = await submit(await mountForm());

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find(".auth-message").text()).toBe("Ошибка сервера");
    });

    // Followed through the real service to the client: only the press sets the flags.
    it("presses with the refusal and the limit taken over from the interceptor", async () => {
      const post = vi
        .spyOn(Api, "post")
        .mockResolvedValue({ data: null, error: null } as never);

      await submit(await mountForm());

      expect(post).toHaveBeenCalledWith(
        "account/recovery",
        { email: "reader@dm.am" },
        expect.objectContaining({ ownsRefusal: true, ownsRateLimit: true }),
      );
    });
  });
});
