/**
 * @vitest-environment jsdom
 */

/**
 * The form reported success for every failure that was not a 400. A banned
 * account got the dialog closed, one toast reading "Недостаточно прав для этого
 * действия", and a header still offering "Вход | Регистрация" — while the
 * reason the server gave reached nobody.
 *
 * The second factor is the same trap in a different disguise, and worse: there
 * the server answers 200. "Now enter the code" is a successful reply with a
 * stage on it and no viewer in it, so a form that reads "no error, therefore a
 * session" closes over a guest and drops a login that was half done. Asserted
 * below on all three stages, because the stage is now the whole answer.
 *
 * The guest surface added three more things that are behaviour and not paint,
 * and each of them is a way for the screen to lie:
 *
 *   - the shape of the address is not this form's business: the browser will
 *     not submit a value that is not an address out of a type="email" field,
 *     and a shape the browser passes tells a login nothing;
 *   - what is wrong is said under the field and nowhere else, so the frame
 *     stays neutral and a second, colour-only carrier cannot drift out of step
 *     with the sentence;
 *   - the code is checked the moment it is whole. A step that waits for a
 *     button after the last digit is a step nobody takes on purpose, and a
 *     check started twice spends two of the few attempts the server allows.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { defineComponent, h, nextTick } from "vue";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { signIn, completeSecondFactor } from "@/entities/user";
import { AuthDialog } from "@/shared/ui/Layout";
import LoginForm from "./LoginForm.vue";
// The component as it is written, for the two rules below that live in the
// source and never reach the rendered tree. Vite hands the file over as text.
import SOURCE from "./LoginForm.vue?raw";

// The second activation letter goes out of this card. The endpoint behind it
// decides for itself which letter an address needs, and for a registration that
// was never finished that is the activation one.
const recover = vi.hoisted(() => vi.fn());

vi.mock("@/entities/user", () => ({
  signIn: vi.fn(),
  completeSecondFactor: vi.fn(),
  accountApi: { recover },
}));

// The way back for a removed account leads off this card: an account is brought
// back by moderation, so the offer is a ticket with the reason already chosen.
const { push } = vi.hoisted(() => ({ push: vi.fn() }));
vi.mock("vue-router", () => ({ useRouter: () => ({ push }) }));

// One instance, so "the form said it and nothing else did" can be asserted.
const toast = vi.hoisted(() => ({
  error: vi.fn(),
  warning: vi.fn(),
  success: vi.fn(),
}));

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => toast,
}));

/**
 * The library tier under AuthDialog. It is mocked rather than stubbed by name:
 * AuthDialog reaches Dialog.vue by import, so nothing registered on the mount
 * could replace it, and the real component wants the vfm plugin installed.
 */
vi.mock("vue-final-modal", () => ({
  VueFinalModal: defineComponent({
    setup:
      (_props, { slots }) =>
      () =>
        h("div", slots.default?.()),
  }),
}));

enableAutoUnmount(afterEach);

const problem = (
  status: number,
  title = "",
  retryAfterSeconds?: number,
): GeneralError => ({
  type: "",
  title,
  status,
  traceId: "trace",
  ...(retryAfterSeconds === undefined ? {} : { retryAfterSeconds }),
});

const refused = (status: number, title = "", retryAfterSeconds?: number) =>
  ({
    stage: "refused",
    failure: problem(status, title, retryAfterSeconds),
  }) as const;

/** The clock the form reads to measure how long it has been on screen. */
let now = 0;

/** Focus and selection are only observable while the form is in a document. */
const mountForm = (prefillEmail?: string) =>
  mount(LoginForm, {
    props: prefillEmail === undefined ? {} : { prefillEmail },
    attachTo: document.body,
  });

async function submitCredentials() {
  const wrapper = mountForm();
  await flushPromises();

  // Past the minimum fill time, so the submit goes out without waiting it out.
  now += 10_000;

  await wrapper.find("#email").setValue("reader@dm.am");
  await wrapper.find("#password").setValue("correct horse");
  await wrapper.find("form").trigger("submit");
  await flushPromises();

  return wrapper;
}

/** Carries the login as far as the code field, with nothing typed into it. */
async function atSecondFactor() {
  vi.mocked(signIn).mockResolvedValue({ stage: "secondFactor" });
  return submitCredentials();
}

/**
 * Types `code` into the field of the second step.
 *
 * There is no submit here on purpose: a whole code starts its own check, and
 * that is the behaviour every test of this step is written against.
 */
async function typeCode(
  wrapper: Awaited<ReturnType<typeof atSecondFactor>>,
  code: string,
) {
  await wrapper.find("#two-factor-code").setValue(code);
  await flushPromises();
}

/**
 * Pastes `text` into the field of the second step, the way a browser would.
 *
 * A browser cuts what is pasted down to the field's own `maxlength` before any
 * event reaches the screen, and jsdom does not: writing the whole string
 * straight into the element, a test cannot tell a field that keeps the length
 * itself from one that leaves the length to the parse. That difference is the
 * subject of the two tests that use this, so the cut is done here.
 */
async function pasteCode(
  wrapper: Awaited<ReturnType<typeof atSecondFactor>>,
  text: string,
) {
  const field = wrapper.find("#two-factor-code");
  const limit = field.attributes("maxlength");

  await field.setValue(limit ? text.slice(0, Number(limit)) : text);
  await flushPromises();
}

/**
 * Presses the segment that names a kind of code.
 *
 * By its caption and not by its position: the two segments are the words the
 * reader chooses between, and a test that clicks the second one would keep
 * passing if they were swapped.
 */
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

/**
 * The link of one label row, found by the label it stands beside.
 *
 * Every row of this card carries one and the card carries three: the address
 * row offers the way back for an account that is gone, the password row the way
 * out of a sign-in that failed, the code row the other kind of code. A search
 * over the whole card takes whichever row the template happens to write first,
 * which is how a test of one row ends up asserting another.
 */
function linkBeside(
  wrapper: Awaited<ReturnType<typeof submitCredentials>>,
  label: string,
) {
  const row = wrapper
    .findAll(".auth-label-row")
    .find((candidate) => candidate.find(".auth-label").text() === label);

  if (!row) throw new Error(`нет строки лейбла "${label}"`);
  return row.find(".auth-action");
}

describe("LoginForm", () => {
  beforeEach(() => {
    now = 0;
    vi.spyOn(Date, "now").mockImplementation(() => now);
    vi.mocked(signIn).mockReset();
    vi.mocked(completeSecondFactor).mockReset();
    recover.mockReset();
    push.mockReset();
    toast.error.mockReset();
    toast.warning.mockReset();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the reason the server refused instead of reporting success", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(403, "Аккаунт забанен"));

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(wrapper.text()).toContain(
      "Аккаунт находится в полном бане бессрочно",
    );
  });

  /**
   * The lockout says when to come back, in the sentence the rate limit uses.
   *
   * It used to say "Вход заблокирован после неудачных попыток": the event, and
   * no way to act on it. The two are different events - fifteen wrong passwords
   * shut this account's door for half an hour, a rate limit is a budget an
   * address spends in a minute - but the answer to both is to wait the stated
   * time, so both say it the same way. The seconds ride with the refusal.
   */
  it("says how long the lockout has left, and shuts the button with it", async () => {
    vi.mocked(signIn).mockResolvedValue(
      refused(403, "Вход заблокирован", 1800),
    );

    const wrapper = await submitCredentials();

    const strip = wrapper.find("#login-password-message");
    expect(strip.text()).toBe(
      "Слишком много попыток, попробуйте через 30 минут",
    );
    // The same counter the rate limit uses, so the same rule under the same
    // sentence: the button is shut while the wait runs. Two refusals worded
    // alike and behaving differently is what reads as a broken form.
    expect(wrapper.find("button.auth-button").attributes("disabled")).toBe("");

    // And nothing is said twice: the request took the refusal over.
    expect(toast.warning).not.toHaveBeenCalled();
  });

  /** A lockout the server sent without a number still has to say something. */
  it("falls back to the wordless wait when the lockout brings no seconds", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(403, "Вход заблокирован"));

    const wrapper = await submitCredentials();

    expect(wrapper.find("#login-password-message").text()).toBe(
      "Слишком много попыток, попробуйте позже",
    );
  });

  /** Any other refusal of the server is printed as the server wrote it. */
  it("leaves a refusal it does not recognise in the server's own words", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(403, "Вход недоступен"));

    const wrapper = await submitCredentials();

    expect(wrapper.find("#login-password-message").text()).toBe(
      "Вход недоступен",
    );
  });

  it("keeps showing what a rejected form got wrong", async () => {
    const rejected: BadRequestError = {
      ...problem(400),
      errors: { password: ["Неверная почта или пароль"] },
    };
    vi.mocked(signIn).mockResolvedValue({
      stage: "refused",
      failure: rejected,
    });

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(wrapper.text()).toContain("Неверная почта или пароль");
  });

  // The sentence is the whole of the report. A field that also turned red said
  // it twice, in a colour a third of readers cannot separate from the resting
  // one, and the two carriers then had to be kept in step by hand.
  it("says what is wrong under the field and leaves the field itself alone", async () => {
    const rejected: BadRequestError = {
      ...problem(400),
      errors: { password: ["Неверная почта или пароль"] },
    };
    vi.mocked(signIn).mockResolvedValue({
      stage: "refused",
      failure: rejected,
    });

    const wrapper = await submitCredentials();

    const message = wrapper.find("#login-password-message");
    expect(message.classes()).toContain("auth-message--error");
    expect(message.text()).toBe("Неверная почта или пароль");
    // Nothing on the field or its wrapper carries the state.
    expect(wrapper.findAll(".auth-field")[1].classes()).toEqual(["auth-field"]);
    expect(wrapper.find("#password").attributes("aria-invalid")).toBe("true");
  });

  // A dead server is said under the password, where every other refusal of
  // the submit stands, in the API's own words, and the plate the interceptor
  // held for it is taken down (the owner's rule of 26 September 2026).
  it("says a server error under the password, and not in a plate", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(500, "Ошибка сервера"));

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(wrapper.find("#login-password-message").text()).toBe(
      "Ошибка сервера",
    );
  });

  // A request that never arrived is about the reader's network: the plate
  // says it, and the form keeps quiet.
  it("leaves a request that never arrived to its plate", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(0, ""));

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(wrapper.find("#login-password-message").text()).toBe("");
  });

  /**
   * The rate limit is the one refusal a reader answers by pressing again, and
   * the way in is where he does it. As a toast it faded after eight seconds
   * over a form that had not changed — button live, strip empty — so the second
   * press bought the same refusal and the form read as broken.
   */
  describe("a rate limit on the way in", () => {
    beforeEach(() => {
      // Only the interval: the form measures its own age with Date.now, which
      // this file already controls, and the bot-protection wait is a setTimeout
      // the submit helper steps past.
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

      const strip = wrapper.find("#login-password-message");
      expect(strip.text()).toBe(
        "Слишком много попыток, попробуйте через 3 секунды",
      );
      expect(strip.classes()).toContain("auth-message--error");
      expect(wrapper.find("button.auth-button").attributes("disabled")).toBe(
        "",
      );
      // One refusal, one carrier. The request took the limit over from the
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

    // The second step spends the same budget as the first, and its check starts
    // by itself on a whole code: without the wait on the gate the field would
    // keep sending into a shut door.
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
      // The hint takes the strip back, which is what it says when nothing is
      // wrong, and the button answers again.
      expect(strip.text()).toBe("Шесть цифр из приложения-аутентификатора");
      expect(
        wrapper.find("button.auth-button").attributes("disabled"),
      ).toBeUndefined();
    });

    // No header, no number to count: the sentence then names no time, and a
    // wait this side invented would be a guess enforced on the reader.
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

    // The modal keeps this component mounted when it closes, so an interval
    // left running ticks on behind a screen nobody is looking at — and shuts
    // the button of the next visit for a wait that expired unobserved.
    it("drops the wait when the dialog closes", async () => {
      vi.mocked(signIn).mockResolvedValue(
        refused(429, "Слишком много запросов", 30),
      );

      const wrapper = await submitCredentials();
      expect(vi.getTimerCount()).toBe(1);

      // The event the modal tier raises on its way out. It is emitted here
      // rather than clicked for, because the tier that raises it is mocked.
      wrapper.findComponent(AuthDialog).vm.$emit("beforeClose");
      await nextTick();

      expect(vi.getTimerCount()).toBe(0);
      expect(wrapper.find("#login-password-message").text()).toBe("");
    });

    // The other half: a form actually taken off the page mid-countdown.
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

    expect(wrapper.emitted("success")).toHaveLength(1);
  });

  describe("the address", () => {
    // An empty field is not a malformed one: the reader has not answered yet,
    // and a form that objects to that objects to being opened.
    it("is not objected to while it is still empty", async () => {
      const wrapper = mountForm();
      await flushPromises();

      await wrapper.find("#email").trigger("blur");
      await flushPromises();

      expect(wrapper.find("#login-email-message").text()).toBe("");
    });

    /**
     * The shape of the address is answered under the field, on the way out of
     * it, the way the rest of the surface answers it.
     *
     * Owner's decision. A mistyped address otherwise earned "Неверная почта или
     * пароль" after a press, and that sentence points at the password as
     * readily as at the address, so the reader retypes the wrong half.
     */
    it("is objected to for its shape, under the field", async () => {
      const wrapper = mountForm();
      await flushPromises();

      await wrapper.find("#email").setValue("lex@dm");
      await wrapper.find("#email").trigger("blur");
      await flushPromises();

      expect(wrapper.find("#login-email-message").text()).toBe(
        "Неверный формат почты",
      );
    });

    // And nothing is sent: a request that cannot succeed spends a try of the
    // address budget and comes back with a sentence about the password.
    it("is not sent while its shape is wrong", async () => {
      const wrapper = mountForm();
      await flushPromises();
      now += 10_000;

      await wrapper.find("#email").setValue("lex@dm");
      await wrapper.find("#password").setValue("correct horse");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(signIn).not.toHaveBeenCalled();
      expect(wrapper.find("#login-email-message").text()).toBe(
        "Неверный формат почты",
      );
    });

    // A well-formed address goes out as typed, and the answer is the one the
    // login has: whether the pair opens the door.
    it("is sent as it was typed, and answered as credentials", async () => {
      const rejected: BadRequestError = {
        ...problem(400),
        errors: { password: ["Неверная почта или пароль"] },
      };
      vi.mocked(signIn).mockResolvedValue({
        stage: "refused",
        failure: rejected,
      });

      const wrapper = mountForm();
      await flushPromises();
      now += 10_000;

      await wrapper.find("#email").setValue("lex@dm.am");
      await wrapper.find("#password").setValue("correct horse");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

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
      const wrapper = mountForm();
      await flushPromises();

      expect(document.activeElement).toBe(wrapper.find("#email").element);
    });

    // An address carried over from the registration or the recovery form is
    // not the thing left to fill in.
    it("skips the address it was handed", async () => {
      const wrapper = mountForm("lex@dm.am");
      await flushPromises();

      expect(document.activeElement).toBe(wrapper.find("#password").element);
    });
  });

  describe("an unfinished registration", () => {
    const pending: BadRequestError = {
      ...problem(400),
      errors: { _pendingActivation: ["true"] },
    };

    const signInPending = () =>
      vi.mocked(signIn).mockResolvedValue({
        stage: "refused",
        failure: pending,
      });

    const resent = () =>
      recover.mockResolvedValue({
        data: { status: "ActivationResent", email: "reader@dm.am" },
        error: null,
      });

    /** The press that used to hand the reader to another card. */
    const askForTheLetter = async (
      wrapper: Awaited<ReturnType<typeof submitCredentials>>,
    ) => {
      await linkBeside(wrapper, "Пароль").trigger("click");
      await flushPromises();
    };

    it("offers the letter again in place of the way out", async () => {
      signInPending();

      const wrapper = await submitCredentials();

      expect(wrapper.text()).toContain("Регистрация не завершена");
      expect(linkBeside(wrapper, "Пароль").text()).toBe(
        "Отправить письмо повторно",
      );
    });

    /**
     * The press sends the letter and says so.
     *
     * It used to open the recovery card, which asked for the address already
     * typed into the field above the link and then sent this very request: one
     * screen, one field and one more press for a letter the reader had already
     * asked for.
     */
    it("sends the letter itself and states the outcome on the same card", async () => {
      signInPending();
      resent();

      const wrapper = await submitCredentials();
      await askForTheLetter(wrapper);

      // With both halves of the answer claimed from the interceptor: the
      // refusal goes into the strip under the password and the wait onto the
      // button, and a toast over that is the same refusal said twice. Claimed
      // at the press and not inside the method, because the activation page
      // sends this very request with nothing of its own to show - a flag fixed
      // in the method leaves its failures announced by nobody. That the method
      // claims nothing on its own is gated in accountApi.spec.ts; this file
      // mocks the service away and sees only what it was asked for.
      expect(recover).toHaveBeenCalledWith("reader@dm.am", {
        ownsRefusal: true,
        ownsRateLimit: true,
      });
      expect(wrapper.text()).toContain("Проверьте почту");
      expect(wrapper.text()).toContain(
        "Мы отправили повторное письмо со ссылкой для активации на",
      );
      expect(wrapper.text()).toContain("reader@dm.am");
      expect(wrapper.text()).toContain("Ссылка действительна 48 часов");
      // The card states an outcome and takes nothing: the sign-in form is gone
      // from under it rather than standing behind the receipt.
      expect(wrapper.find("#password").exists()).toBe(false);
    });

    /**
     * A receipt is about the address that was typed for it, and the next visit
     * opens on a card that has not been typed into. The modal keeps this
     * component mounted, so the receipt has to be taken down by hand.
     */
    it("takes the receipt down when the dialog closes", async () => {
      signInPending();
      resent();

      const wrapper = await submitCredentials();
      await askForTheLetter(wrapper);
      expect(wrapper.text()).toContain("Проверьте почту");

      // The event the modal tier raises on its way out. It is emitted here
      // rather than clicked for, because the tier that raises it is mocked.
      wrapper.findComponent(AuthDialog).vm.$emit("beforeClose");
      await nextTick();

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#password").exists()).toBe(true);
    });

    /** One press, one letter: the second one is not sent while the first flies. */
    it("does not send a second letter while the first is on the wire", async () => {
      signInPending();
      recover.mockImplementation(() => new Promise(() => {}));

      const wrapper = await submitCredentials();
      await askForTheLetter(wrapper);
      await askForTheLetter(wrapper);

      expect(recover).toHaveBeenCalledTimes(1);
    });

    /**
     * A receipt is a promise that a letter went out, so it is put up on that
     * answer and on no other. The refusal takes the strip the sign-in answers
     * in, and the card stays where the reader can press again.
     */
    it("says the letter did not go out instead of promising one", async () => {
      signInPending();
      recover.mockResolvedValue({
        data: null,
        error: problem(500, "Ошибка сервера"),
      });

      const wrapper = await submitCredentials();
      await askForTheLetter(wrapper);

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#login-password-message").text()).toBe(
        "Ошибка сервера",
      );
    });

    /**
     * The address stopped being an unfinished registration between the refusal
     * and the press: the endpoint sent a password reset or found nobody, and
     * neither is the letter this card promised.
     */
    it("promises no activation letter when another one went out", async () => {
      signInPending();
      recover.mockResolvedValue({
        data: { status: "PasswordResetSent", email: "reader@dm.am" },
        error: null,
      });

      const wrapper = await submitCredentials();
      await askForTheLetter(wrapper);

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#login-password-message").text()).toBe(
        "Не удалось отправить письмо",
      );
    });

    it("goes back to the ordinary way out as soon as anything is typed", async () => {
      signInPending();

      const wrapper = await submitCredentials();
      await wrapper.find("#email").setValue("reader@dm.ru");

      expect(linkBeside(wrapper, "Пароль").text()).toBe("Не могу войти");
    });
  });

  /**
   * The account itself, and not the form: both halves of the credentials were
   * accepted and what stands in the way is what moderation did. The API throws
   * exactly two such refusals, and it throws both from either half of the
   * login — a ban issued in the minutes between the factors lands on the code
   * step — so the form says them in one wording on both steps.
   *
   * And it says them where it says every other answer to the submit: in the
   * strip under the field the reader last filled in. A box of its own over the
   * card was a second place to look for one thing, and the reader who found
   * the strip empty read the form as one that had answered nothing.
   */
  describe("an account the server refused", () => {
    /** A 403 that may carry the end of the ban, as the API may name it. */
    type Banned = GeneralError & { banExpiresUtc?: string | null };

    const banned = (banExpiresUtc?: string | null): Banned => {
      const failure = problem(403, "Аккаунт забанен");
      return banExpiresUtc === undefined
        ? failure
        : { ...failure, banExpiresUtc };
    };

    const removed = () => problem(403, "Аккаунт удален");

    // The sentence the owner settled, in the strip every other refusal of this
    // form stands in. The server's own "Аккаунт забанен" names the event; this
    // names it to the person it stopped.
    it("spells the ban out in the strip, and not in a box of its own", async () => {
      vi.mocked(signIn).mockResolvedValue({
        stage: "refused",
        failure: banned(),
      });

      const wrapper = await submitCredentials();

      const strip = wrapper.find("#login-password-message");
      expect(strip.text()).toBe("Аккаунт находится в полном бане бессрочно");
      expect(strip.classes()).toContain("auth-message--error");
      // One refusal, one carrier, and the card is not remade around it.
      expect(wrapper.find(".auth-warnbox").exists()).toBe(false);
      expect(toast.error).not.toHaveBeenCalled();
    });

    // The term is the part the reader acts on, and the date is spelled the way
    // the site spells every other date.
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

    // No end to name, said out loud rather than left as a sentence that trails
    // off: a ban with no term is the one a reader most needs told plainly. The
    // answer that names no term at all — the one the API sends today — is the
    // same sentence: two states of the document, one fact for the reader.
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

    // A removed account is told in the same one strip as a ban, and it is the
    // one refusal of the two with somewhere to go next, so it takes the link
    // above the strip with it.
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

    // The strip of the code step is the one place that step answers in, so the
    // refusal takes it over from the hint, and the step stops asking: every
    // further code would spend an attempt against a door shut for another
    // reason.
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

    // The removed account is answered there in the same words, and the label
    // row goes the same way it goes on the first step: no code opens a removed
    // account, so the kind of code has stopped being a question and the row
    // carries the way back instead.
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

    // The refusal belongs to the account that was named, and the name is being
    // changed: what is written now is about somebody else.
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

  /**
   * The way back for an account that is gone.
   *
   * It answers one refusal and stands only with it, in the place the card keeps
   * for whatever the state calls for — beside the password, above the strip that
   * just said the account was removed. It gives nothing away by being there: the
   * strip beside it has already said it out loud, and that answer costs a
   * correct password, so a stranger never reaches the state at all. The request
   * it leads to is not filed here — an account is removed and brought back by
   * moderation alone — so the card hands the reader to support with the reason
   * already chosen.
   */
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

    // A ban is the other refusal about the account itself, and it has nowhere
    // to go: the row keeps the standing way out.
    it("is not offered to an account that was banned", async () => {
      vi.mocked(signIn).mockResolvedValue(refused(403, "Аккаунт забанен"));

      const wrapper = await submitCredentials();

      expect(linkBeside(wrapper, "Пароль").text()).toBe("Не могу войти");
    });

    // The refusal is about the account that was named, and the name is being
    // rewritten: what stood for it goes with it.
    it("goes away as soon as the address is edited", async () => {
      const wrapper = await refuseAsRemoved();

      await wrapper.find("#email").setValue("lexx@dm.am");
      await flushPromises();

      expect(linkBeside(wrapper, "Пароль").text()).toBe("Не могу войти");
    });

    // The support form opens on the category this card is about, so the reader
    // does not pick it out of a list to say what the previous screen knew. The
    // dialog closes first: the page must not open behind a dimmed overlay.
    it("closes the dialog and opens support on the restoration category", async () => {
      const wrapper = await refuseAsRemoved();

      await linkBeside(wrapper, "Пароль").trigger("click");
      await flushPromises();

      expect(wrapper.emitted("cancel")).toHaveLength(1);
      expect(push).toHaveBeenCalledWith("/support?subtype=AccountRestoration");
    });
  });

  describe("second factor", () => {
    it("asks for the code instead of reporting a session", async () => {
      const wrapper = await atSecondFactor();

      expect(wrapper.emitted("success")).toBeUndefined();
      expect(wrapper.text()).toContain("Подтверждение входа");
      expect(wrapper.find("#two-factor-code").exists()).toBe(true);
      // The password step is gone: this dialog now holds one field.
      expect(wrapper.find("#password").exists()).toBe(false);
    });

    // The step swaps inside a dialog that is already open, so the button the
    // reader pressed is gone and focus falls back to the document. Somebody on
    // a keyboard would have to find their way back into a form they never left.
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

    // Nobody reaches for a button after the last digit of a code; every second
    // factor on the web checks it where it stands.
    it("checks a whole code without being asked", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(null);
      const wrapper = await atSecondFactor();

      await typeCode(wrapper, "123456");

      expect(completeSecondFactor).toHaveBeenCalledWith("123456");
      expect(wrapper.emitted("success")).toHaveLength(1);
    });

    // Waiting is the site's one state: the control stops answering, wears the
    // disabled hatch and says aria-busy. The caption is not part of it: a
    // "Проверяем..." swapped in mid-request is a second wording of one action,
    // and it re-measures the button while the reader is aiming at it.
    // UI_STANDARDS says so and busyControls.spec.ts holds it shut.
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

    // The parse is what keeps the length of a code, and it reads what actually
    // arrived: a line of a letter with the code inside it leaves the code.
    it("keeps the whole code out of a paste longer than the field", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(null);
      const wrapper = await atSecondFactor();

      await pasteCode(wrapper, "код из письма: 123456");

      expect(
        (wrapper.find("#two-factor-code").element as HTMLInputElement).value,
      ).toBe("123456");
    });

    // The auto-check reads a full field as "he is done typing", and a recovery
    // code pasted into the digit field leaves exactly a full field of digits
    // behind: six symbols that look like a code from the authenticator and are
    // not one. That is the reaction this screen was written for, and a
    // maxlength on the field put it out of reach: the browser cut the paste
    // first, and the parse then read a fragment instead of a code.
    it("does not check by itself a recovery code pasted into the digit field", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(null);
      const wrapper = await atSecondFactor();

      await pasteCode(wrapper, "7K3M-9Q2W-XZ8P-4R6T");

      expect(
        (wrapper.find("#two-factor-code").element as HTMLInputElement).value,
      ).toBe("739284");
      expect(completeSecondFactor).not.toHaveBeenCalled();
      // And the button is not taken away with the auto-check: what is in the
      // field is his to send, he just has to be the one who says so.
      expect(
        wrapper.find(".auth-button").attributes("disabled"),
      ).toBeUndefined();

      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(completeSecondFactor).toHaveBeenCalledWith("739284");
    });

    // Nothing was thrown away, so nothing is being second-guessed: the dashes
    // of a recovery code are the field's own and a code pasted with them is
    // still exactly the code.
    it("checks by itself a code pasted with the dashes it draws", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(null);
      const wrapper = await atSecondFactor();

      await chooseCodeKind(wrapper, "Резервный код");
      await typeCode(wrapper, "7K3M-9Q2W-XZ8P-4R6T");

      expect(completeSecondFactor).toHaveBeenCalledWith("7K3M9Q2WXZ8P4R6T");
    });

    // The button and the Enter key are one path, since both submit the form,
    // and it is the path a code that arrived by paste takes.
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

    // One check per code. A second one started while the first is out spends
    // an attempt the server counts and the reader never made.
    it("does not start a second check over the first", async () => {
      vi.mocked(completeSecondFactor).mockReturnValue(new Promise(() => {}));
      const wrapper = await atSecondFactor();

      await typeCode(wrapper, "123456");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(completeSecondFactor).toHaveBeenCalledTimes(1);
    });

    // One sentence answers all five ways of failing this step — a wrong code,
    // an expired challenge, one nobody issued, a spent recovery code, a
    // challenge out of attempts — and the screen says it in its own words:
    // "Неверный код" is the server's vocabulary, and what the reader is told
    // is which of the two things in front of him is wrong.
    it("says the code is wrong, in the words of this screen", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(
        problem(400, "Неверный код"),
      );

      const wrapper = await atSecondFactor();
      await typeCode(wrapper, "123456");

      expect(wrapper.emitted("success")).toBeUndefined();
      // In the strip the hint stood in, so the card does not change height.
      const message = wrapper.find("#two-factor-code-message");
      expect(message.text()).toBe("Неверный код");
      expect(message.classes()).toContain("auth-message--error");
    });

    // The next attempt is typed over the last one, and there is nothing to
    // clear by hand first.
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

    // A refusal is about the code that earned it. The moment that code is
    // edited the sentence is about nothing, and the strip goes back to the
    // hint.
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

    // Backspace in front of a dash is the one edit the field makes itself: the
    // key would otherwise delete a separator the reformat puts straight back,
    // so the composable takes the symbol before it and writes the field by
    // hand. A value written by hand fires no input event, and the refusal the
    // reader is busy correcting outlived the code it was about.
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

      // The caret rests immediately after the first dash of "7K3M-9Q2W-...".
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

    // The kind of code is named by the label and swapped by the link beside
    // it, and the choice remakes the field: what it accepts, what a soft
    // keyboard offers, and how the typed code is spaced. One value goes to the
    // server either way, and the server tells the two kinds apart by shape.
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

    // How long a code is is not one of the things the field carries. The
    // browser applies maxlength before an event reaches this screen, so a
    // recovery code pasted into the digit field arrived already cut, and the
    // screen's own answer to that paste could never be reached by a person.
    it("leaves the length of the code to the parse, in both kinds", async () => {
      const wrapper = await atSecondFactor();
      const field = () => wrapper.find("#two-factor-code");

      expect(field().attributes("maxlength")).toBeUndefined();

      await chooseCodeKind(wrapper, "Резервный код");

      expect(field().attributes("maxlength")).toBeUndefined();
    });

    // The label names the kind being typed and the link offers the other one,
    // so the field is named the way every other field of the surface is, and
    // the pair reads as one sentence rather than as two buttons.
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

    // What was typed belonged to the other kind of code, and rewritten under
    // the new rules it leaves a fragment nobody entered.
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

    // The kind of code was a pair of links under the button, each naming the
    // other kind, and the reader had to work out which of the two he was in.
    // What was wrong there was the pair and the silence about the current kind,
    // not the words on the links: the label above the field now says the kind
    // being typed, and one link beside it offers the other. Guarded as the pair
    // it was, because an assertion against the wording alone forbids the plain
    // way to write the link and leaves the defect itself allowed.
    it("offers one switch, beside the label that names the kind", async () => {
      const wrapper = await atSecondFactor();

      expect(wrapper.findAll(".auth-label-row .auth-action")).toHaveLength(1);
      expect(wrapper.text()).toContain("Ввести резервный код");
      expect(wrapper.text()).not.toContain("Ввести код из приложения");
    });

    // Sixteen symbols on screen carry three dashes the field put there itself;
    // the server is given the code and not the formatting.
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

    // The foot of the card carries one way out, and it names where it leads
    // instead of asking the question the reader has already answered.
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

/**
 * Two things the rendered tree cannot answer for, read off the source instead.
 *
 * The look of a control is not behaviour, but a look drawn twice is: the guest
 * surface draws the link-shaped action once, and a scoped copy of that recipe
 * drifts from it silently and is emitted again into this component's own
 * sheet. So the form has no stylesheet at all, and that is asserted as itself.
 * The rule it replaces read a scoped sass block and looked for declarations
 * after an include; with no block to read it was green whatever the form did.
 */
describe("the source of the form", () => {
  // This form has five escape hatches: "Не могу войти", the offer of the
  // letter again, "Восстановить аккаунт", the other kind of code, and
  // "Отключить двухфакторную аутентификацию". Each of them acts — two of
  // them close the dialog and route from the handler — so they stay <button>
  // (linkDestinations.spec.ts sends an anchor with nowhere to go back to one)
  // and take the surface's own class rather than a private reissue of its look.
  it("takes the link-shaped action from the guest surface instead of drawing its own", () => {
    expect(SOURCE).not.toContain("+inline-link-button");
    // Seven and not five: the label row of the password carries whichever of
    // its three links the state calls for and the row of the code step either
    // of its two, each written out, and the foot of the first step carries the
    // way to the registration.
    expect(SOURCE.match(/class="auth-action"/g)).toHaveLength(7);
  });

  it("carries no stylesheet of its own", () => {
    expect(SOURCE).not.toContain("<style");
  });
});
