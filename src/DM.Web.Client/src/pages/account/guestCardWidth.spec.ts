/**
 * @vitest-environment jsdom
 */

/**
 * How wide every screen of the guest contour asks to be.
 *
 * Width follows what a card holds, not the tier it opens in: the wide measure is
 * kept where an address or a password is typed and where a refusal explains
 * itself, and everything else that states an outcome is narrow. The
 * tier does not decide it. The same "mark, heading, one line, button" card is
 * narrow as a dialog and narrow again as a page from a letter, which is why the
 * dialogs and the pages are walked here in one file.
 *
 * The sheet used to work the width out from the contents of the card. It could
 * not, and it failed silently: the width of a dialog sits on the wrapper, so
 * the wrapper's rule had one :has inside another, and a browser drops such a
 * rule whole. Nothing on screen said so, and no test of the sheet could either,
 * because the selector was still written there in full.
 *
 * So the screen says which of the two it wants, and this file is the register
 * of what each of them said. One assertion per state on purpose: a state that
 * stopped declaring anything is the defect, and it hides in a table of states
 * nobody enumerated.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import { defineComponent, h, nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { accountApi, UsernameInput } from "@/entities/user";
import {
  AccessRecoveryForm,
  LoginForm,
  RegistrationForm,
  RegistrationSuccess,
  TwoFactorRemovalForm,
} from "@/features/auth";
import AccountActivationPage from "./AccountActivationPage.vue";
import PasswordResetPage from "./PasswordResetPage.vue";
import TwoFactorRemovalPage from "./TwoFactorRemovalPage.vue";
import TwoFactorRemovalCancelPage from "./TwoFactorRemovalCancelPage.vue";

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

/**
 * The library tier under AuthDialog. Mocked rather than stubbed by name:
 * AuthDialog reaches Dialog.vue by import, so nothing registered on the mount
 * could replace it, and the real component wants the vfm plugin installed. The
 * dialog wrapper itself is left alone, because it is half of the subject.
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

const options = () => ({
  global: {
    plugins: [createPinia()],
    stubs: { RouterLink: { template: "<a><slot /></a>" } },
  },
});

/** A request that never settles: the screen stays in the state under test. */
const pending = () => new Promise(() => {}) as never;

/** Which of the two measures the card on screen asked for. */
const measure = (wrapper: VueWrapper): "narrow" | "wide" =>
  wrapper.get(".auth-card").classes().includes("auth-card--compact")
    ? "narrow"
    : "wide";

// ───────────────────────────────────────────────────────────── the dialogs ──

const registrationSent = async () => {
  const wrapper = mount(RegistrationSuccess, {
    props: { email: "reader@dm.am" },
    ...options(),
  });
  await flushPromises();
  return wrapper as VueWrapper;
};

const recoveryAsked = async () => {
  const wrapper = mount(AccessRecoveryForm, options());
  await flushPromises();
  return wrapper as VueWrapper;
};

const recoveryAnswered = (status: string) => async () => {
  vi.spyOn(accountApi, "recover").mockResolvedValue({
    data: { status, email: "reader@dm.am" },
    error: null,
  } as never);

  const wrapper = await recoveryAsked();
  await wrapper.get("#recovery-email").setValue("reader@dm.am");
  await wrapper.get("form").trigger("submit");
  await flushPromises();
  return wrapper;
};

/** The offer that stands in the label row of an unfinished registration. */
const resendLetter = async (wrapper: VueWrapper) => {
  const offer = wrapper
    .findAll("button")
    .find((button) => button.text() === "Отправить письмо повторно");

  if (!offer) throw new Error('нет кнопки "Отправить письмо повторно"');
  await offer.trigger("click");
  await flushPromises();
};

/**
 * The second activation letter goes out of the sign-in card itself, and the
 * card turns into the receipt for it. Two forms end this way, and both of them
 * are wide until they do: the form under the receipt is the widest thing in the
 * contour, so a receipt that forgets to say the word keeps that width.
 */
const loginResent = async () => {
  vi.spyOn(accountApi, "signIn").mockResolvedValue({
    data: null,
    error: {
      type: "",
      title: "",
      status: 400,
      traceId: "trace",
      errors: { _pendingActivation: ["true"] },
    },
  } as never);
  vi.spyOn(accountApi, "recover").mockResolvedValue({
    data: { status: "ActivationResent", email: "reader@dm.am" },
    error: null,
  } as never);

  // The card waits out the rest of three seconds on screen before it sends
  // anything, so the clock is moved past them rather than waited through.
  let clock = 0;
  vi.spyOn(Date, "now").mockImplementation(() => clock);

  const wrapper = mount(LoginForm, options()) as VueWrapper;
  await flushPromises();
  clock += 10_000;
  await wrapper.get("#email").setValue("reader@dm.am");
  await wrapper.get("#password").setValue("correct horse");
  await wrapper.get("form").trigger("submit");
  await flushPromises();

  await resendLetter(wrapper);
  return wrapper;
};

const registrationResent = async () => {
  vi.spyOn(accountApi, "checkEmail").mockResolvedValue({
    data: { isAvailable: false, reason: "PendingActivation" },
    error: null,
  } as never);
  vi.spyOn(accountApi, "recover").mockResolvedValue({
    data: { status: "ActivationResent", email: "reader@dm.am" },
    error: null,
  } as never);

  const wrapper = mount(RegistrationForm, options()) as VueWrapper;
  await wrapper.get("#registration-email").setValue("reader@dm.am");
  await wrapper.get("#registration-email").trigger("blur");
  await flushPromises();

  await resendLetter(wrapper);
  return wrapper;
};

const removalAsked = async () => {
  const wrapper = mount(TwoFactorRemovalForm, options());
  await flushPromises();
  return wrapper as VueWrapper;
};

const removalRequested = async () => {
  vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
    data: {},
    error: null,
  } as never);

  const wrapper = await removalAsked();
  await wrapper.get("#two-factor-removal-email").setValue("reader@dm.am");
  await wrapper.get("form").trigger("submit");
  await flushPromises();
  return wrapper;
};

// ───────────────────────────────────────────── the pages from the letters ──

const READY = {
  data: { status: "ready" as const, email: "reader@dm.am" },
  error: null,
};
const EXPIRED = {
  data: { status: "expired" as const, email: "reader@dm.am" },
  error: null,
};

const activation = (info: unknown) => async () => {
  vi.spyOn(accountApi, "getActivationInfo").mockResolvedValue(info as never);

  const wrapper = mount(AccountActivationPage, options());
  await flushPromises();
  return wrapper as VueWrapper;
};

const activationChecking = async () => {
  vi.spyOn(accountApi, "getActivationInfo").mockImplementation(pending);
  return mount(AccountActivationPage, options()) as VueWrapper;
};

const activationNaming = activation(READY);

const activationConfirming = async () => {
  const wrapper = await activationNaming();
  const name = wrapper.findComponent(UsernameInput);

  name.vm.$emit("update:modelValue", "reader");
  name.vm.$emit("availability", true);
  await nextTick();
  await wrapper.get(".submit-button").trigger("click");
  return wrapper;
};

const activationDone = async () => {
  const wrapper = await activationConfirming();
  vi.spyOn(accountApi, "activate").mockResolvedValue({
    data: { resource: { id: "1", username: "reader" } },
    error: null,
  } as never);

  await wrapper.get(".submit-button").trigger("click");
  await flushPromises();
  return wrapper;
};

const reset = (answer: unknown) => async () => {
  vi.spyOn(accountApi, "getPasswordResetTokenInfo").mockResolvedValue(
    answer as never,
  );

  const wrapper = mount(PasswordResetPage, options());
  await flushPromises();
  return wrapper as VueWrapper;
};

const resetChecking = async () => {
  vi.spyOn(accountApi, "getPasswordResetTokenInfo").mockImplementation(pending);
  return mount(PasswordResetPage, options()) as VueWrapper;
};

const resetAsking = reset({ data: { status: "ready" }, error: null });

const resetSaved = async () => {
  const wrapper = await resetAsking();
  vi.spyOn(accountApi, "completePasswordReset").mockResolvedValue({
    data: {},
    error: null,
  } as never);

  await wrapper.get("#reset-new-password").setValue("correct horse");
  await wrapper.get("form").trigger("submit");
  await flushPromises();
  return wrapper;
};

const removalConfirming = async () => {
  const wrapper = mount(TwoFactorRemovalPage, options());
  await flushPromises();
  return wrapper as VueWrapper;
};

const removalScheduled = async () => {
  vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
    data: {},
    error: null,
  } as never);

  const wrapper = await removalConfirming();
  await wrapper.get(".auth-button").trigger("click");
  await flushPromises();
  return wrapper;
};

const removalDeadLink = async () => {
  // A link with nothing in it: the page answers that without asking anybody.
  window.location.hash = "";
  const wrapper = mount(TwoFactorRemovalPage, options());
  await flushPromises();
  return wrapper as VueWrapper;
};

const cancelDeadLink = async () => {
  window.location.hash = "";
  const wrapper = mount(TwoFactorRemovalCancelPage, options());
  await flushPromises();
  return wrapper as VueWrapper;
};

const cancelRunning = async () => {
  vi.spyOn(accountApi, "cancelTwoFactorRemoval").mockImplementation(pending);
  return mount(TwoFactorRemovalCancelPage, options()) as VueWrapper;
};

const cancelDone = async () => {
  vi.spyOn(accountApi, "cancelTwoFactorRemoval").mockResolvedValue({
    data: {},
    error: null,
  } as never);

  const wrapper = mount(TwoFactorRemovalCancelPage, options());
  await flushPromises();
  return wrapper as VueWrapper;
};

// ─────────────────────────────────────────────────────────── the register ──

/** Every state that states an outcome, whichever tier it states it in. */
const NARROW: readonly [string, () => Promise<VueWrapper>][] = [
  ['диалог "Проверьте почту" после регистрации', registrationSent],
  [
    'диалог "Проверьте почту" после входа в незавершенную регистрацию',
    loginResent,
  ],
  [
    'диалог "Проверьте почту" о повторной активации из регистрации',
    registrationResent,
  ],
  ['диалог "Проверьте почту" о сбросе пароля', recoveryAnswered("Reset")],
  [
    'диалог "Проверьте почту" о повторной активации',
    recoveryAnswered("ActivationResent"),
  ],
  ['диалог "Аккаунт не найден"', recoveryAnswered("NotFound")],
  ['диалог "Проверьте почту" об отключении защиты', removalRequested],
  ['активация, "Проверяем ссылку"', activationChecking],
  ['активация, "Выберите имя"', activationNaming],
  ['активация, "Подтвердите выбор имени"', activationConfirming],
  ['активация, "Регистрация завершена"', activationDone],
  ['активация, "Ссылка устарела"', activation(EXPIRED)],
  ['сброс пароля, "Проверяем ссылку"', resetChecking],
  ['сброс пароля, "Новый пароль сохранен"', resetSaved],
  [
    'сброс пароля, "Ссылка устарела"',
    reset({ data: { status: "expired" }, error: null }),
  ],
  ['отключение защиты, "Подтвердите отключение"', removalConfirming],
  ['отключение защиты, "Отключение назначено"', removalScheduled],
  ['отмена отключения, "Отменяем отключение"', cancelRunning],
  ['отмена отключения, "Отключение отменено"', cancelDone],
  // Six screens answer a dead link with one heading, one sentence and one
  // button, so six carry one width. The two ends of the mailed removal used to
  // widen for a heading they no longer show ("Ссылка отключения устарела",
  // 314px against the 302 a narrow card gives); "Ссылка недействительна"
  // occupies 270px and fits.
  [
    'активация, "Ссылка недействительна"',
    activation({ data: null, error: { status: 404 } }),
  ],
  [
    'сброс пароля, "Ссылка недействительна"',
    reset({ data: null, error: { status: 404 } }),
  ],
  ['отключение защиты, "Ссылка недействительна"', removalDeadLink],
  ['отмена отключения, "Ссылка недействительна"', cancelDeadLink],
];

/** Everything that takes an address or a password, or lists causes. */
const WIDE: readonly [string, () => Promise<VueWrapper>][] = [
  ['диалог "Восстановление доступа"', recoveryAsked],
  ['диалог "Запрос на отключение"', removalAsked],
  ['сброс пароля, "Новый пароль"', resetAsking],
];

describe("the width every guest screen asks for", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    sessionStorage.clear();
    localStorage.clear();
    vi.restoreAllMocks();
    // The value arrives in the fragment, and readConfirmationToken clears it
    // from the address as it reads it, so every state puts it back.
    window.location.hash = "#token=a-token";
  });

  it.each(NARROW)("narrows %s", async (_, open) => {
    expect(measure(await open())).toBe("narrow");
  });

  it.each(WIDE)("keeps the wide measure on %s", async (_, open) => {
    expect(measure(await open())).toBe("wide");
  });

  /**
   * The width of a dialog sits on the wrapper and not on the card, so the lit
   * rectangle and the card are one rectangle and a click beside the card closes
   * the dialog. The two therefore carry one word, from one prop.
   */
  it("narrows the lit rectangle of a dialog together with its card", async () => {
    const wrapper = await registrationSent();

    expect(wrapper.get(".auth-dialog").classes()).toContain(
      "auth-dialog--compact",
    );
    expect(wrapper.get(".auth-card").classes()).toContain("auth-card--compact");
  });
});
