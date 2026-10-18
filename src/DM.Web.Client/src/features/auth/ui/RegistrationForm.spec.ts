/**
 * @vitest-environment jsdom
 */

/**
 * The two screens a visitor signs up through, and the four gates on the way.
 *
 * The address is asked for first and shown back as a plate on the second
 * screen, so the reader can see what the letter is about to be sent to. The
 * rules are a door that has to be opened before it can be agreed to. The one
 * thing the browser cannot judge about a password — whether it stands in a
 * breach index — is judged by the server, and its answer takes the line of the
 * strength scale and holds the button shut until a different password is typed.
 *
 * And the oldest of the four: the form opened the "Проверьте почту" screen for
 * every failure that was not a 400 — a rate limit, a mail server that would not
 * take the letter — and the reader waited for a letter nobody had sent.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { defineComponent, h, nextTick } from "vue";
import type {
  ApiResult,
  BadRequestError,
  GeneralError,
} from "@/shared/api/models/common";
import type { EmailAvailability } from "@/shared/api/models/account";
import { Tooltip } from "@/shared/ui/Tooltip";
import { accountApi, register } from "@/entities/user";
import RegistrationForm from "./RegistrationForm.vue";

// One instance, so "the card said it and nothing else did" can be asserted.
const toast = vi.hoisted(() => ({
  error: vi.fn(),
  warning: vi.fn(),
  success: vi.fn(),
}));

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => toast,
}));

vi.mock("vue-router", () => ({ useRouter: () => ({ push: vi.fn() }) }));
// The second activation letter goes out of this card now. The endpoint behind
// it decides for itself which letter an address needs, and for a registration
// that was never finished that is the activation one.
vi.mock("@/entities/user", () => ({
  register: vi.fn(),
  accountApi: { checkEmail: vi.fn(), recover: vi.fn() },
}));

/** The dialog frame is a vue-final-modal wrapper; the card inside it is the subject. */
const AuthDialogStub = defineComponent({
  name: "AuthDialog",
  setup:
    (_props, { slots }) =>
    () =>
      h("div", slots.default?.()),
});

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

/** A 400 that names the field it is about, the way the API answers one. */
const fieldProblem = (field: string, code: string): BadRequestError => ({
  type: "",
  title: "Проверьте введенные данные",
  status: 400,
  traceId: "trace",
  errors: { [field]: [code] },
});

/** What the availability check answers with. */
type CheckEmailAnswer = ApiResult<EmailAvailability>;

/** The clock the form reads to measure how long it has been on screen. */
let now = 0;

function mountForm() {
  return mount(RegistrationForm, {
    global: { stubs: { AuthDialog: AuthDialogStub } },
  });
}

type Wrapper = ReturnType<typeof mountForm>;

/** The card's secondary actions are buttons and are found by what they say. */
const control = (wrapper: Wrapper, caption: string) =>
  wrapper.findAll("button").find((button) => button.text() === caption);

async function toPasswordStep(wrapper: Wrapper, email = "reader@dm.am") {
  await wrapper.find("#registration-email").setValue(email);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
  // Past the minimum fill time, so the submit goes out instead of asking to wait.
  now += 10_000;
}

/** Opening the rules is what unlocks the box that accepts them. */
async function acceptRules(wrapper: Wrapper) {
  await wrapper.find('a[href="/rules"]').trigger("click");
  await wrapper.find("#registration-rules").setValue(true);
}

async function submitRegistration(password = "correct horse battery") {
  const wrapper = mountForm();
  await toPasswordStep(wrapper);

  await wrapper.find("#registration-password").setValue(password);
  await acceptRules(wrapper);
  await wrapper.find("form").trigger("submit");
  await flushPromises();

  return wrapper;
}

describe("RegistrationForm", () => {
  beforeEach(() => {
    now = 0;
    sessionStorage.clear();
    vi.spyOn(Date, "now").mockImplementation(() => now);
    vi.mocked(accountApi.checkEmail).mockReset();
    vi.mocked(accountApi.checkEmail).mockResolvedValue({
      data: { isAvailable: true },
      error: null,
    });
    vi.mocked(register).mockReset();
    vi.mocked(accountApi.recover).mockReset();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("does not promise a letter the server never took", async () => {
    vi.mocked(register).mockResolvedValue(problem(500, "Ошибка сервера"));

    const wrapper = await submitRegistration();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(sessionStorage.getItem("dm_pending_email")).toBeNull();
    // Nothing is written into the card: a 500 is already on screen as the
    // interceptor's toast, and the card would say it a second time.
    expect(wrapper.find(".registration-refusal").exists()).toBe(false);
  });

  /**
   * The rate limit used to be a toast and nothing else: it faded after eight
   * seconds over a card that had not changed — button live, nothing written —
   * so the reader pressed again, bought the same refusal, and concluded the
   * form was broken. Both steps of this card are on the same address budget,
   * and the first of them goes out on every submit of the address.
   */
  describe("a rate limit", () => {
    beforeEach(() => {
      // Only the interval: the card measures its own age with Date.now, which
      // this file already controls.
      vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it("stands under the address when the check was refused", async () => {
      vi.mocked(accountApi.checkEmail).mockResolvedValue({
        data: null,
        error: problem(429, "Слишком много запросов", 3),
      });

      const wrapper = mountForm();
      await wrapper.find("#registration-email").setValue("reader@dm.am");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      // Still on the first step: the limit refused the check, so nothing is
      // known about this address yet.
      expect(wrapper.find("#registration-email").exists()).toBe(true);
      const strip = wrapper.find("#registration-email-message");
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

    it("takes the line of the scale when the registration was refused", async () => {
      vi.mocked(register).mockResolvedValue(
        problem(429, "Слишком много запросов", 3),
      );

      const wrapper = await submitRegistration();

      expect(wrapper.emitted("success")).toBeUndefined();
      expect(sessionStorage.getItem("dm_pending_email")).toBeNull();
      // The card has one place for a message about this step, and the scale
      // line is it. The bar turns red with the line.
      const refusal = wrapper.find(".password-strength .warning-text");
      expect(refusal.text()).toBe(
        "Слишком много попыток, попробуйте через 3 секунды",
      );
      expect(wrapper.find(".strength-fill").classes()).toContain("error");
      expect(wrapper.find("button.auth-button").attributes("disabled")).toBe(
        "",
      );
      expect(toast.warning).not.toHaveBeenCalled();

      vi.advanceTimersByTime(3000);
      await nextTick();
      // The countdown is over: the line is back to the strength verdict and the
      // bar to its own colour.
      expect(wrapper.find(".password-strength .warning-text").exists()).toBe(
        false,
      );
      expect(wrapper.find(".password-strength .strength-text").exists()).toBe(
        true,
      );
      expect(
        wrapper.find("button.auth-button").attributes("disabled"),
      ).toBeUndefined();
    });

    // The modal keeps this component mounted when it closes, so an interval
    // left running ticks on behind a screen nobody is looking at.
    it("drops the wait when the dialog closes", async () => {
      vi.mocked(register).mockResolvedValue(
        problem(429, "Слишком много запросов", 30),
      );

      const wrapper = await submitRegistration();
      expect(vi.getTimerCount()).toBe(1);

      wrapper.findComponent({ name: "AuthDialog" }).vm.$emit("beforeClose");
      await nextTick();

      expect(vi.getTimerCount()).toBe(0);
    });

    it("drops the wait when the card leaves the page", async () => {
      vi.mocked(register).mockResolvedValue(
        problem(429, "Слишком много запросов", 30),
      );

      const wrapper = await submitRegistration();
      expect(vi.getTimerCount()).toBe(1);

      wrapper.unmount();

      expect(vi.getTimerCount()).toBe(0);
    });
  });

  it("says what the server named when it refused the address", async () => {
    vi.mocked(register).mockResolvedValue(
      problem(409, "Почта уже зарегистрирована"),
    );

    const wrapper = await submitRegistration();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(wrapper.text()).toContain("Почта уже зарегистрирована");
  });

  it("hands the address on once the registration is pending", async () => {
    vi.mocked(register).mockResolvedValue(null);

    const wrapper = await submitRegistration();

    expect(wrapper.emitted("success")?.[0]).toEqual(["reader@dm.am"]);
    expect(sessionStorage.getItem("dm_pending_email")).toBe("reader@dm.am");
  });

  it("states the length rule before it can be broken", async () => {
    const wrapper = mountForm();
    await toPasswordStep(wrapper);

    // An untouched password field, and the rule is already on screen: the scale
    // stands on the guest forms from the start.
    expect(wrapper.find(".password-strength").exists()).toBe(true);
    expect(wrapper.text()).toContain("Минимум 8 символов");
  });

  it("keeps the rules box shut until the rules were opened", async () => {
    const wrapper = mountForm();
    await toPasswordStep(wrapper);

    const box = wrapper.find("#registration-rules");
    expect(box.attributes("disabled")).toBeDefined();

    // The tooltip is what explains the dead box, and it stops explaining once
    // there is nothing left to explain.
    const tooltip = wrapper.findComponent(Tooltip);
    expect(tooltip.props("text")).toBe("Сначала откройте правила сайта");
    expect(tooltip.props("disabled")).toBe(false);

    await wrapper.find('a[href="/rules"]').trigger("click");

    expect(wrapper.find("#registration-rules").attributes("disabled")).toBe(
      undefined,
    );
    expect(wrapper.findComponent(Tooltip).props("disabled")).toBe(true);
  });

  it("does not register anybody who has not accepted the rules", async () => {
    vi.mocked(register).mockResolvedValue(null);

    const wrapper = mountForm();
    await toPasswordStep(wrapper);
    await wrapper.find("#registration-password").setValue("correct horse");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(register).not.toHaveBeenCalled();

    await acceptRules(wrapper);
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(register).toHaveBeenCalledTimes(1);
  });

  it("puts the server's word about the password in the line of the scale", async () => {
    vi.mocked(register).mockResolvedValue(
      fieldProblem("password", "Пароль найден в утечке, выберите другой"),
    );

    const wrapper = await submitRegistration();

    expect(wrapper.text()).toContain("Пароль найден в утечке, выберите другой");
    expect(wrapper.find(".strength-fill").classes()).toContain("error");

    // The refusal is about this password, so the form stays shut on it: sending
    // the same one again would only earn the same answer.
    await wrapper.find("form").trigger("submit");
    await flushPromises();
    expect(register).toHaveBeenCalledTimes(1);

    // A different password is one the server has not seen.
    await wrapper.find("#registration-password").setValue("another passphrase");
    expect(wrapper.text()).not.toContain("Пароль найден в утечке");
    await wrapper.find("form").trigger("submit");
    await flushPromises();
    expect(register).toHaveBeenCalledTimes(2);
  });

  it("offers the way in when the address is already somebody's", async () => {
    vi.mocked(accountApi.checkEmail).mockResolvedValue({
      data: { isAvailable: false, reason: "Taken" },
      error: null,
    });

    const wrapper = mountForm();
    await wrapper.find("#registration-email").setValue("reader@dm.am");
    await wrapper.find("#registration-email").trigger("blur");
    await flushPromises();

    expect(wrapper.text()).toContain("Аккаунт с этой почтой уже существует");

    const login = control(wrapper, "Войти");
    expect(login).toBeDefined();
    await login?.trigger("click");

    expect(wrapper.emitted("openLogin")?.[0]).toEqual(["reader@dm.am"]);
  });

  /**
   * The same address, the same answer, whichever step learns it.
   *
   * The check on blur can be skipped: a reader who types the address and hits
   * Enter reaches the server, and the refusal comes back as a field code. It
   * used to print the sentence alone - the address is taken, and no way to the
   * account holding it, on the one screen where that way is a single click.
   */
  it("offers the way in when the submit is what learns the address is taken", async () => {
    vi.mocked(register).mockResolvedValue({
      ...problem(400, ""),
      errors: { email: ["Taken"] },
    } as never);

    const wrapper = await submitRegistration();

    expect(wrapper.text()).toContain("Аккаунт с этой почтой уже существует");

    const login = control(wrapper, "Войти");
    expect(login).toBeDefined();
    await login?.trigger("click");
    expect(wrapper.emitted("openLogin")?.[0]).toEqual(["reader@dm.am"]);
  });

  /**
   * The address belongs to a registration nobody finished, and the one thing
   * worth offering is the letter again.
   *
   * The press used to open the recovery card, which asked for the address
   * already typed into the field beside the link and then sent this very
   * request. It sends it from here now, and the card states the outcome itself.
   */
  describe("a registration that was never finished", () => {
    /** The verdict that puts the offer of the letter into the label row. */
    const pending = () =>
      vi.mocked(accountApi.checkEmail).mockResolvedValue({
        data: { isAvailable: false, reason: "PendingActivation" },
        error: null,
      });

    const resent = () =>
      vi.mocked(accountApi.recover).mockResolvedValue({
        data: { status: "ActivationResent", email: "reader@dm.am" },
        error: null,
      } as never);

    /** Types the address and waits out the check the blur starts. */
    const askAbout = async (email = "reader@dm.am") => {
      const wrapper = mountForm();
      await wrapper.find("#registration-email").setValue(email);
      await wrapper.find("#registration-email").trigger("blur");
      await flushPromises();
      return wrapper;
    };

    const askForTheLetter = async (wrapper: Wrapper) => {
      await control(wrapper, "Отправить письмо повторно")?.trigger("click");
      await flushPromises();
    };

    it("offers the letter again and nothing else", async () => {
      pending();

      const wrapper = await askAbout();

      expect(wrapper.text()).toContain("Регистрация не завершена");
      expect(control(wrapper, "Отправить письмо повторно")).toBeDefined();
      // The way in belongs to a taken address alone.
      expect(control(wrapper, "Войти")).toBeUndefined();
    });

    it("sends the letter itself and states the outcome on the same card", async () => {
      pending();
      resent();

      const wrapper = await askAbout();
      await askForTheLetter(wrapper);

      // With both halves of the answer claimed from the interceptor: the
      // refusal goes into the strip under the address and the wait onto the
      // button, and a toast over that is the same refusal said twice. Claimed
      // at the press and not inside the method, because the activation page
      // sends this very request with nothing of its own to show - a flag fixed
      // in the method leaves its failures announced by nobody. That the method
      // claims nothing on its own is gated in accountApi.spec.ts; this file
      // mocks the service away and sees only what it was asked for.
      expect(accountApi.recover).toHaveBeenCalledWith("reader@dm.am", {
        ownsRefusal: true,
        ownsRateLimit: true,
      });
      expect(wrapper.emitted("openRecovery")).toBeUndefined();
      expect(wrapper.text()).toContain("Проверьте почту");
      expect(wrapper.text()).toContain(
        "Мы отправили повторное письмо со ссылкой для активации на",
      );
      expect(wrapper.text()).toContain("reader@dm.am");
      expect(wrapper.text()).toContain("Ссылка действительна 48 часов");
      // The receipt takes the card: the form is gone from under it rather than
      // standing behind it.
      expect(wrapper.find("#registration-email").exists()).toBe(false);
    });

    /**
     * A receipt is about the address that was typed for it, and the next visit
     * opens on a card nobody has typed into. The modal keeps this component
     * mounted, so the receipt goes down with the rest of the form.
     */
    it("takes the receipt down when the dialog closes", async () => {
      pending();
      resent();

      const wrapper = await askAbout();
      await askForTheLetter(wrapper);
      expect(wrapper.text()).toContain("Проверьте почту");

      // The event the modal tier raises on its way out, emitted rather than
      // clicked for: the tier that raises it is stubbed.
      wrapper.findComponent(AuthDialogStub).vm.$emit("beforeClose");
      await nextTick();

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#registration-email").exists()).toBe(true);
    });

    /** One press, one letter: the second is not sent while the first flies. */
    it("does not send a second letter while the first is on the wire", async () => {
      pending();
      vi.mocked(accountApi.recover).mockImplementation(
        () => new Promise(() => {}) as never,
      );

      const wrapper = await askAbout();
      await askForTheLetter(wrapper);
      await askForTheLetter(wrapper);

      expect(accountApi.recover).toHaveBeenCalledTimes(1);
    });

    /**
     * A receipt is a promise that a letter went out, so it is put up on that
     * answer and on no other. The refusal takes the strip under the field, and
     * the card stays where the reader can press again.
     */
    it("says the letter did not go out instead of promising one", async () => {
      pending();
      vi.mocked(accountApi.recover).mockResolvedValue({
        data: null,
        error: problem(500, "Ошибка сервера"),
      } as never);

      const wrapper = await askAbout();
      await askForTheLetter(wrapper);

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#registration-email-message").text()).toBe(
        "Ошибка сервера",
      );
    });

    /**
     * The address stopped being an unfinished registration between the check
     * and the press: the endpoint sent a password reset or found nobody, and
     * neither is the letter this card offered.
     */
    it("promises no activation letter when another one went out", async () => {
      pending();
      vi.mocked(accountApi.recover).mockResolvedValue({
        data: { status: "PasswordResetSent", email: "reader@dm.am" },
        error: null,
      } as never);

      const wrapper = await askAbout();
      await askForTheLetter(wrapper);

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#registration-email-message").text()).toBe(
        "Не удалось отправить письмо",
      );
    });
  });

  /**
   * Two verdicts about an address, and no third. A removed account's address
   * answers as a live one's: the server says "Taken" for both, because
   * "удален" is a decision about an account and a guest at this form is not
   * owed it. The owner of a removed account is shown the way to support after
   * a correct password on the sign-in card, or by the recovery letter. This
   * form used to keep a third branch for "RemovedAccount", with a sentence and
   * a "Восстановить?" link of its own; the value is off the wire, and a server
   * still sending it reads as taken, the way every reason that is not
   * "PendingActivation" does.
   */
  it.each([
    ["Taken", "the reason the wire carries"],
    ["RemovedAccount", "a value the wire no longer carries"],
  ])("answers %s with the way in and no third verdict, %s", async (reason) => {
    vi.mocked(accountApi.checkEmail).mockResolvedValue({
      data: { isAvailable: false, reason } as EmailAvailability,
      error: null,
    });

    const wrapper = mountForm();
    await wrapper.find("#registration-email").setValue("reader@dm.am");
    await wrapper.find("#registration-email").trigger("blur");
    await flushPromises();

    expect(wrapper.text()).toContain("Аккаунт с этой почтой уже существует");
    expect(wrapper.text()).not.toContain("удален");
    expect(control(wrapper, "Восстановить?")).toBeUndefined();
    // One link in the label row, and it is the way in.
    expect(
      wrapper.findAll(".auth-label-row button").map((button) => button.text()),
    ).toEqual(["Войти"]);
  });

  /**
   * Every link in the label row is an offer about the address that was asked
   * about: "Войти" hands it to the sign-in card, "Отправить письмо повторно"
   * asks for the letter again. Left standing over an address the reader has
   * begun rewriting, the link carries the old one away.
   */
  it.each([
    ["Taken", "Войти"],
    ["PendingActivation", "Отправить письмо повторно"],
  ])(
    "takes down the link of a %s address on the first character typed",
    async (reason, caption) => {
      vi.mocked(accountApi.checkEmail).mockResolvedValue({
        data: { isAvailable: false, reason } as EmailAvailability,
        error: null,
      });

      const wrapper = mountForm();
      const email = wrapper.find("#registration-email");
      await email.setValue("lexx@dm.am");
      await email.trigger("blur");
      await flushPromises();

      expect(control(wrapper, caption)).toBeDefined();

      await email.setValue("lexx2@dm.am");

      expect(control(wrapper, caption)).toBeUndefined();
      expect(wrapper.find("#registration-email-message").text()).toBe("");
    },
  );

  /**
   * The address the server takes is a hundred characters at most: the rule in
   * UserRegistrationValidator, and the column behind it. The field carries the
   * same ceiling, so the refusal for a longer one cannot be earned: the browser
   * stops the typing and cuts the paste before anything is sent, and nobody is
   * told off for something only the field could have prevented.
   */
  it("stops the address at the length the server takes", () => {
    const wrapper = mountForm();

    expect(wrapper.find("#registration-email").attributes("maxlength")).toBe(
      "100",
    );
  });

  /**
   * The address was free when the check ran and taken by the time the
   * registration went out, which is also what a check the rate limit refused
   * leaves behind. The server answers in codes, and the shared vocabulary reads
   * "Taken" as "Уже занято": true of a name as much as of an address, and
   * naming neither. The card says what stands in the way, in the same words the
   * check on blur uses.
   */
  it("names what stands in the way when the server refuses the address", async () => {
    vi.mocked(register).mockResolvedValue(fieldProblem("email", "Taken"));

    const wrapper = await submitRegistration();

    expect(wrapper.emitted("success")).toBeUndefined();
    // Back on the address step, with the answer in the strip under the field.
    expect(wrapper.find("#registration-email").exists()).toBe(true);
    expect(wrapper.find("#registration-email-message").text()).toBe(
      "Аккаунт с этой почтой уже существует",
    );
    expect(wrapper.text()).not.toContain("Уже занято");
  });

  it("shows the address it is about, and lets the reader back to it", async () => {
    const wrapper = mountForm();
    await toPasswordStep(wrapper);

    expect(wrapper.find(".auth-value").text()).toBe("reader@dm.am");
    expect(wrapper.find("#registration-email").exists()).toBe(false);

    await control(wrapper, "Изменить")?.trigger("click");

    expect(
      (wrapper.find("#registration-email").element as HTMLInputElement).value,
    ).toBe("reader@dm.am");
  });

  it("takes the agreement and the policy by the act, not by a second box", async () => {
    const wrapper = mountForm();
    await toPasswordStep(wrapper);

    expect(wrapper.findAll('input[type="checkbox"]')).toHaveLength(1);
    expect(wrapper.find(".auth-footnote").text()).toBe(
      "Регистрируясь, вы принимаете пользовательское соглашение и политику конфиденциальности",
    );
  });

  it("checks the address once when Enter arrives twice", async () => {
    const wrapper = mountForm();
    const email = wrapper.find("#registration-email");
    await email.setValue("reader@dm.am");

    // Enter is bound to the field, and a held key repeats the keydown. The
    // second press lands while the first check is still on the wire, so
    // nothing but the gate inside submitEmail can stop it.
    void email.trigger("keydown.enter");
    void email.trigger("keydown.enter");
    await flushPromises();

    expect(accountApi.checkEmail).toHaveBeenCalledTimes(1);
  });

  it("registers once when the submit is repeated", async () => {
    vi.mocked(register).mockResolvedValue(null);

    const wrapper = mountForm();
    await toPasswordStep(wrapper);
    await wrapper.find("#registration-password").setValue("correct horse");
    await acceptRules(wrapper);

    // What a held Enter does on the second screen: the form is submitted again
    // while the first registration is still in flight. Two of them are two
    // activation letters, or a race the server answers with a 409.
    const form = wrapper.find("form");
    void form.trigger("submit");
    void form.trigger("submit");
    await flushPromises();

    expect(register).toHaveBeenCalledTimes(1);
  });

  it("wears the surface's own link-button on its secondary actions", async () => {
    const wrapper = mountForm();

    // The recipe belongs to the guest surface, which paints it for every one of
    // these screens at once. A class of this card's own would be a second copy
    // of it, free to drift from the login's.
    expect(control(wrapper, "восстановлением доступа")?.classes()).toContain(
      "auth-action",
    );
    expect(control(wrapper, "поддержку")?.classes()).toContain("auth-action");
    expect(wrapper.find(".auth-link").exists()).toBe(false);

    await toPasswordStep(wrapper);
    expect(control(wrapper, "Изменить")?.classes()).toContain("auth-action");
  });

  it("points each field at the line standing under it", async () => {
    vi.mocked(accountApi.checkEmail).mockResolvedValue({
      data: { isAvailable: false, reason: "Taken" },
      error: null,
    });

    const wrapper = mountForm();
    const email = wrapper.find("#registration-email");
    expect(email.attributes("aria-describedby")).toBe(
      "registration-email-message",
    );
    expect(wrapper.find("#registration-email-message").exists()).toBe(true);
    // Nothing is wrong yet, so the field is not marked as holding a refusal.
    expect(email.attributes("aria-invalid")).toBeUndefined();

    await email.setValue("reader@dm.am");
    await email.trigger("blur");
    await flushPromises();

    expect(wrapper.find("#registration-email").attributes("aria-invalid")).toBe(
      "true",
    );
  });

  it("points the password at the scale, and marks the refusal on the field", async () => {
    vi.mocked(register).mockResolvedValue(
      fieldProblem("password", "Пароль найден в утечке, выберите другой"),
    );

    const wrapper = mountForm();
    await toPasswordStep(wrapper);

    const password = wrapper.find("#registration-password");
    expect(password.attributes("aria-describedby")).toBe(
      "registration-password-message",
    );
    expect(wrapper.find("#registration-password-message").text()).toContain(
      "Минимум 8 символов",
    );
    expect(password.attributes("aria-invalid")).toBeUndefined();

    await password.setValue("correct horse battery");
    await acceptRules(wrapper);
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(wrapper.find("#registration-password-message").text()).toContain(
      "Пароль найден в утечке, выберите другой",
    );
    expect(
      wrapper.find("#registration-password").attributes("aria-invalid"),
    ).toBe("true");
  });

  it("puts the caret in the first field the moment the card opens", async () => {
    // Focus is only real for an element that is in the document, so this one
    // card is mounted into it.
    const wrapper = mount(RegistrationForm, {
      global: { stubs: { AuthDialog: AuthDialogStub } },
      attachTo: document.body,
    });
    await flushPromises();

    expect(document.activeElement).toBe(
      wrapper.find("#registration-email").element,
    );

    wrapper.unmount();
  });

  it("sits out the minimum fill time instead of refusing the submit", async () => {
    vi.mocked(register).mockResolvedValue(null);

    const wrapper = mountForm();
    await wrapper.find("#registration-email").setValue("reader@dm.am");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    // The card is younger than the minimum fill time — which is what a password
    // manager does to it, and what the reader used to be refused for.
    now = 1900;

    await wrapper.find("#registration-password").setValue("correct horse");
    await acceptRules(wrapper);
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    // Nothing is said at the reader: the button is waiting out the remainder,
    // the way the login waits it out.
    expect(wrapper.text()).not.toContain("Подождите");
    expect(register).not.toHaveBeenCalled();
    expect(wrapper.find("button.auth-button").attributes("aria-busy")).toBe(
      "true",
    );

    await new Promise((resolve) => setTimeout(resolve, 150));
    await flushPromises();

    expect(register).toHaveBeenCalledTimes(1);
    expect(wrapper.emitted("success")?.[0]).toEqual(["reader@dm.am"]);
  });

  /**
   * The press that arrives while the address is being checked.
   *
   * Pressing the button blurs the field, and the blur is what starts the
   * availability check — so the press itself lands while the answer is still on
   * the wire. The button was dead for exactly that stretch, so the press fell
   * into the gap and the reader pressed a second time to do what the first
   * press had already asked for.
   */
  it("takes the first press while the check is still on the wire", async () => {
    let answer: ((value: CheckEmailAnswer) => void) | undefined;
    vi.mocked(accountApi.checkEmail).mockImplementation(
      () =>
        new Promise<CheckEmailAnswer>((resolve) => {
          answer = resolve;
        }),
    );

    const wrapper = mountForm();
    const email = wrapper.find("#registration-email");
    await email.setValue("reader@dm.am");

    // What the press does before it is a press. The button has to be live at
    // this moment, or the click never reaches the form at all.
    await email.trigger("blur");
    expect(
      wrapper.find("button.auth-button").attributes("disabled"),
    ).toBeUndefined();

    await wrapper.find("form").trigger("submit");
    await flushPromises();

    // The press was taken: the button is waiting rather than switched off, and
    // the step has not moved because the answer is not back yet.
    expect(wrapper.find("button.auth-button").attributes("aria-busy")).toBe(
      "true",
    );
    expect(wrapper.find("#registration-email").exists()).toBe(true);

    answer?.({ data: { isAvailable: true }, error: null });
    await flushPromises();

    // One press, one request, and the second step.
    expect(accountApi.checkEmail).toHaveBeenCalledTimes(1);
    expect(wrapper.find("#registration-password").exists()).toBe(true);
  });

  it("says the one general sentence when the server named no reason", async () => {
    // A refusal with nothing in it: no field, no title. There is nothing to
    // relay, so the card says the sentence the site says everywhere else.
    vi.mocked(register).mockResolvedValue(problem(409));

    const wrapper = await submitRegistration();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(wrapper.find(".password-strength .warning-text").text()).toBe(
      "Не удалось выполнить запрос, попробуйте позже",
    );
  });

  /**
   * One rule for every red line on this card: it lights when the reader is done
   * with the field — left it, or pressed the button — and goes out on the first
   * character typed into that same field. Nothing turns red under the typing
   * hand, so a half-typed address is not called wrong before it is finished.
   *
   * The strength scale is outside the rule: its line is the length rule and the
   * verdict, and both are answers about what is in the field right now.
   */
  it("lights the red only when the field is done with, and drops it on the next keystroke", async () => {
    vi.mocked(accountApi.checkEmail).mockResolvedValue({
      data: { isAvailable: false, reason: "Taken" },
      error: null,
    });

    const wrapper = mountForm();
    const email = wrapper.find("#registration-email");

    // Half of an address, typed. Nothing is wrong yet, because nothing is
    // finished yet.
    await email.setValue("reader@");
    expect(wrapper.find("#registration-email-message").classes()).not.toContain(
      "auth-message--error",
    );

    await email.setValue("reader@dm.am");
    await email.trigger("blur");
    await flushPromises();

    const strip = wrapper.find("#registration-email-message");
    expect(strip.text()).toBe("Аккаунт с этой почтой уже существует");
    expect(strip.classes()).toContain("auth-message--error");

    // Typing into the same field is the answer to the refusal, so the refusal
    // goes: the server has not seen what is in the field now.
    await email.setValue("reader2@dm.am");
    expect(wrapper.find("#registration-email-message").text()).toBe("");
    expect(wrapper.find("#registration-email-message").classes()).not.toContain(
      "auth-message--error",
    );
  });

  it("does not light the red on an answer the reader has already typed past", async () => {
    // The check is slower than the reader: he leaves the field, comes back and
    // keeps typing, and the answer about the address he had lands afterwards.
    // Written out then, it is red appearing under the typing hand, about an
    // address no longer in the field, with a "Войти" beside it that would
    // carry the wrong one away.
    let answer: ((value: CheckEmailAnswer) => void) | undefined;
    vi.mocked(accountApi.checkEmail).mockImplementation(
      () =>
        new Promise<CheckEmailAnswer>((resolve) => {
          answer = resolve;
        }),
    );

    const wrapper = mountForm();
    const email = wrapper.find("#registration-email");
    await email.setValue("reader@dm.am");
    await email.trigger("blur");

    await email.setValue("reader2@dm.am");

    answer?.({ data: { isAvailable: false, reason: "Taken" }, error: null });
    await flushPromises();

    const strip = wrapper.find("#registration-email-message");
    expect(strip.text()).toBe("");
    expect(strip.classes()).not.toContain("auth-message--error");
    expect(control(wrapper, "Войти")).toBeUndefined();
  });
});
