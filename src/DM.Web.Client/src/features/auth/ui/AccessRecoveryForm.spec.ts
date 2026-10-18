/**
 * @vitest-environment jsdom
 */

/**
 * One address, three answers, and one way out of the dialog altogether.
 *
 * The three answers are the reason the card exists, and the fourth control on
 * it is the reason this file does: "Нет доступа к почте?" is the only thing on
 * the screen that leads off it. Wired to an event nobody listens for, the
 * button looks alive, takes the press and does nothing - and it is pressed by
 * exactly the reader who has already run out of other ways back. It leads to
 * the support form, at the address the registration card offers for the same
 * trouble.
 *
 * Two more properties are one line away from being lost. The caret starts in
 * the field, so a reader who opened the dialog can type; and every refusal of
 * the press is said under the field in the server's own sentence - a generic
 * phrase in its place throws away the only description of what went wrong, and
 * a toast in its place fades over a card that has not changed.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { Api } from "@/shared/api";
import AccessRecoveryForm from "./AccessRecoveryForm.vue";

const { push } = vi.hoisted(() => ({ push: vi.fn() }));
vi.mock("vue-router", () => ({ useRouter: () => ({ push }) }));

// One instance, so "the card said it and nothing else did" can be asserted.
const toast = vi.hoisted(() => ({
  error: vi.fn(),
  warning: vi.fn(),
  success: vi.fn(),
}));

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => toast,
}));

enableAutoUnmount(afterEach);

/**
 * The dialog frame is a vue-final-modal wrapper; the card inside it is the
 * subject. Attached to the document, because focus is only observable there.
 */
const mountForm = (prefillEmail?: string) =>
  mount(AccessRecoveryForm, {
    props: prefillEmail === undefined ? {} : { prefillEmail },
    attachTo: document.body,
    global: {
      plugins: [createPinia()],
      stubs: {
        AuthDialog: { name: "AuthDialog", template: "<div><slot /></div>" },
      },
    },
  });

type Wrapper = ReturnType<typeof mountForm>;

/** The card's secondary actions are buttons and are found by what they say. */
const control = (wrapper: Wrapper, caption: string) =>
  wrapper.findAll("button").find((button) => button.text() === caption);

const answered = (status: string) =>
  vi.spyOn(accountApi, "recover").mockResolvedValue({
    data: { status, email: "reader@dm.am" },
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
    setActivePinia(createPinia());
    vi.restoreAllMocks();
    push.mockClear();
  });

  it("asks for one address and names what the button will do", () => {
    const wrapper = mountForm();

    expect(wrapper.text()).toContain("Восстановление доступа");
    expect(wrapper.find("#recovery-email").exists()).toBe(true);
    expect(control(wrapper, "Отправить письмо")).toBeDefined();
  });

  /**
   * The button used to stay grey until the address passed the format check, so
   * a reader who had mistyped it was left in front of a dead control with
   * nothing on the card to say why. The press is what asks the question now,
   * and the strip under the field carries the answer: an empty field still
   * shuts the button, because there is nothing to send and nothing to say.
   */
  it("opens the button on anything typed and names the format on the press", async () => {
    const send = answered("PasswordResetSent");
    const wrapper = mountForm();
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
      const wrapper = mountForm();
      await flushPromises();

      expect(document.activeElement).toBe(
        wrapper.find("#recovery-email").element,
      );
    });

    // The address carried over from the login form fills the only field there
    // is, so there is no emptier one to prefer: the caret stays where the
    // reader may still have to correct it.
    it("keeps the caret there when the address was handed over", async () => {
      const wrapper = mountForm("lex@dm.am");
      await flushPromises();

      const field = wrapper.find("#recovery-email").element as HTMLInputElement;
      expect(field.value).toBe("lex@dm.am");
      expect(document.activeElement).toBe(field);
    });
  });

  // The reader who presses this one has already lost the account and now the
  // mailbox: nothing on this card can help him, and a button that swallows the
  // press leaves him with no way on at all.
  describe("no access to the mailbox either", () => {
    // The ticket opens on the category this card is about, so the reader does
    // not pick out of a list the answer the previous screen already had.
    it("closes the dialog and opens support on the recovery category", async () => {
      const wrapper = mountForm();

      await control(wrapper, "Нет доступа к почте?")?.trigger("click");
      await flushPromises();

      expect(wrapper.emitted("cancel")).toHaveLength(1);
      expect(push).toHaveBeenCalledWith("/support?subtype=AccessRecovery");
    });
  });

  // Both secondary actions of the card take the shared look of the guest
  // surface. A local copy of the recipe drifts from it on the next change.
  it("dresses its secondary actions in the surface class", async () => {
    const wrapper = mountForm();

    expect(control(wrapper, "Нет доступа к почте?")?.classes()).toContain(
      "auth-action",
    );

    answered("NotFound");
    await submit(wrapper);

    // The way back from the empty answer is the card's main action now, so the
    // class it takes is the button's and not the inline link's.
    expect(control(wrapper, "Попробовать другую почту")?.classes()).toContain(
      "auth-button",
    );
  });

  describe("the three answers", () => {
    it("names the reset letter it sent", async () => {
      answered("PasswordResetSent");
      const wrapper = await submit(mountForm());

      expect(wrapper.text()).toContain("Проверьте почту");
      expect(wrapper.text()).toContain("со ссылкой для сброса пароля");
    });

    it("names the activation letter instead when that is what went out", async () => {
      answered("ActivationResent");
      const wrapper = await submit(mountForm());

      expect(wrapper.text()).toContain("со ссылкой для активации");
    });

    /**
     * Both letters out of this card are read by somebody who cannot get in, and
     * the reset link used to expire a day before the activation one. Two
     * windows meant two sentences on two receipts that are otherwise the same
     * screen, and the shorter of them for no reason a reader could be told.
     */
    it("gives both letters the same two days", async () => {
      answered("PasswordResetSent");
      const reset = await submit(mountForm());
      expect(reset.text()).toContain("Ссылка действительна 48 часов");
      expect(reset.text()).not.toContain("24 часа");

      answered("ActivationResent");
      const activation = await submit(mountForm());
      expect(activation.text()).toContain("Ссылка действительна 48 часов");
    });

    // Nothing is left to do on a receipt, and the cross of the dialog frame is
    // the way out: a "Закрыть" button is a second way out that has to be aimed
    // at. The empty answer keeps its button, because it offers the field back.
    it("leaves no button on either receipt", async () => {
      answered("PasswordResetSent");
      const reset = await submit(mountForm());
      expect(reset.findAll("button")).toHaveLength(0);

      answered("ActivationResent");
      const activation = await submit(mountForm());
      expect(activation.findAll("button")).toHaveLength(0);
    });

    it("says there is no such account and offers the field back", async () => {
      answered("NotFound");
      const wrapper = await submit(mountForm());

      expect(wrapper.text()).toContain("Аккаунт не найден");

      await control(wrapper, "Попробовать другую почту")?.trigger("click");
      expect(wrapper.find("#recovery-email").exists()).toBe(true);
    });
  });

  describe("a refusal", () => {
    /**
     * The rate limit used to be a toast and nothing else: it faded after eight
     * seconds over a card that had not changed — button live, strip empty — so
     * the reader pressed again, bought the same refusal, and concluded the form
     * was broken. It now stands in the strip the card already reserves, with
     * the wait running down and the button shut until it is out.
     */
    describe("a rate limit", () => {
      beforeEach(() => {
        // Only the interval: everything else in this file runs on real time.
        vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
      });

      afterEach(() => {
        vi.useRealTimers();
      });

      it("stands under the field, shuts the button, and runs down", async () => {
        refused(429, "Слишком много запросов", 3);
        const wrapper = await submit(mountForm());

        expect(wrapper.text()).not.toContain("Проверьте почту");
        const strip = wrapper.find(".auth-message");
        expect(strip.text()).toBe(
          "Слишком много попыток, попробуйте через 3 секунды",
        );
        expect(strip.classes()).toContain("auth-message--error");
        expect(wrapper.find("button.auth-button").attributes("disabled")).toBe(
          "",
        );
        // One refusal, one carrier: the request took the limit over from the
        // interceptor precisely so this stays empty.
        expect(toast.warning).not.toHaveBeenCalled();

        vi.advanceTimersByTime(1000);
        await nextTick();
        expect(strip.text()).toBe(
          "Слишком много попыток, попробуйте через 2 секунды",
        );

        vi.advanceTimersByTime(2000);
        await nextTick();
        expect(strip.text()).toBe("");
        expect(
          wrapper.find("button.auth-button").attributes("disabled"),
        ).toBeUndefined();
      });

      // The modal keeps this component mounted when it closes, so an interval
      // left running ticks on behind a screen nobody is looking at.
      it("drops the wait when the dialog closes", async () => {
        refused(429, "Слишком много запросов", 30);
        const wrapper = await submit(mountForm());
        expect(vi.getTimerCount()).toBe(1);

        wrapper.findComponent({ name: "AuthDialog" }).vm.$emit("beforeClose");
        await nextTick();

        expect(vi.getTimerCount()).toBe(0);
        expect(wrapper.find(".auth-message").text()).toBe("");
      });

      it("drops the wait when the card leaves the page", async () => {
        refused(429, "Слишком много запросов", 30);
        const wrapper = await submit(mountForm());
        expect(vi.getTimerCount()).toBe(1);

        wrapper.unmount();

        expect(vi.getTimerCount()).toBe(0);
      });
    });

    // The server already said it better than a generic phrase could.
    it("says under the field what the server said", async () => {
      refused(400, "Неверный формат почты");
      const wrapper = await submit(mountForm());

      expect(wrapper.text()).not.toContain("Проверьте почту");
      const strip = wrapper.find(".auth-message");
      expect(strip.text()).toBe("Неверный формат почты");
      expect(strip.classes()).toContain("auth-message--error");
    });

    /**
     * A server that could not post the letter answers 500, and the interceptor
     * says so in a toast that fades after eight seconds over a card that has
     * not changed - so the refusal was lost and the reader was left waiting for
     * a letter nobody sent. It stands in the strip now, like every other
     * refusal of this form.
     */
    it("says under the field that the letter did not go out", async () => {
      refused(500, "Ошибка сервера");
      const wrapper = await submit(mountForm());

      expect(wrapper.text()).not.toContain("Проверьте почту");
      const strip = wrapper.find(".auth-message");
      expect(strip.text()).toBe("Ошибка сервера");
      expect(strip.classes()).toContain("auth-message--error");
    });

    /**
     * A press that got no answer, and a press answered with nothing in it, have
     * no title to relay - and the card used to invent one: "Произошла ошибка.
     * Попробуйте позже." standing next to the server's own "Ошибка сервера"
     * gave one failure two names, and the invented one was two sentences with a
     * full stop apiece. One wording now, the one the API titles a 500 with.
     */
    it("says the same thing when nothing readable came back", async () => {
      refused(0, "");
      const wrapper = await submit(mountForm());

      expect(wrapper.find(".auth-message").text()).toBe("Ошибка сервера");
      expect(wrapper.text()).not.toContain("Попробуйте позже");
    });

    it("says the same thing when the answer carried no payload", async () => {
      vi.spyOn(accountApi, "recover").mockResolvedValue({
        data: null,
        error: null,
      } as never);
      const wrapper = await submit(mountForm());

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find(".auth-message").text()).toBe("Ошибка сервера");
    });

    /**
     * Every refusal above is printed in the strip, and it is one sentence and
     * not two only because the request asked the response interceptor to keep
     * out of this one. That arrangement is held in three places and each of the
     * other two is blind to this card: the client stays quiet for a request
     * carrying the flags (client.spec.ts), the account method carries what a
     * call site hands it and claims nothing of its own (accountApi.spec.ts),
     * and neither knows which call this form presses. The tests above are blind
     * to it as well - they answer in place of the service, so the flags never
     * reach a client at all.
     *
     * So the press is followed through the real service down to the client
     * here, and this is the test that holds the flags of this card since they
     * left the method: fixed there, they also silenced the activation page,
     * which presses the same endpoint and shows nothing of its own. Sent
     * without them, a 500 puts "Ошибка сервера" in the strip and the same
     * sentence in a plate above it, and a 429 leaves a toast fading over a
     * button that is still counting down.
     */
    it("presses with the refusal and the limit taken over from the interceptor", async () => {
      const post = vi
        .spyOn(Api, "post")
        .mockResolvedValue({ data: null, error: null } as never);

      await submit(mountForm());

      expect(post).toHaveBeenCalledWith(
        "account/recovery",
        { email: "reader@dm.am" },
        expect.objectContaining({ ownsRefusal: true, ownsRateLimit: true }),
      );
    });
  });
});
