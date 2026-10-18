/**
 * @vitest-environment jsdom
 */

/**
 * Which of the account requests answer a refusal themselves.
 *
 * A 403 from the sign-in endpoint is the answer to the sign-in form, not a
 * site-wide refusal: the server names the state of the account and the form
 * shows that sentence under the password field. The response interceptor's
 * generic "Недостаточно прав для этого действия" would put a sentence that
 * names nothing in its place, so that one request takes the refusal over.
 *
 * A 429 is the same trap with a worse ending. It used to be a toast and nothing
 * else: it faded after eight seconds over a form that had not changed — button
 * live, strip empty — so the reader pressed again, bought the same refusal, and
 * concluded the form was broken. The guest forms now answer the limit
 * themselves, with the wait running down on the button, and the interceptor
 * keeps quiet — but only for a request that says `ownsRateLimit`.
 *
 * Which makes the flag the joint of the whole arrangement. Dropped from one of
 * these four, the form still shows its sentence and the toast comes back over
 * it, and no test of a form can see that: their transport is mocked out. It is
 * seen here. The other six are named too, so the list is a decision and not an
 * accident: they are sent from screens behind a session, where the
 * interceptor's toast is still the whole answer.
 *
 * The last block is the other half of the same arrangement, and the one the
 * four above are the exception to: who speaks is the call site's to decide, not
 * the service's. One endpoint is read from a screen that answers for it and
 * from a screen that shows nothing - the state of the second factor is a card
 * in the account block and a discarded read on a profile page - so a flag fixed
 * inside the method decides for both, and the screen that shows nothing is left
 * with nobody saying anything at all. Those methods carry what they are handed
 * and add nothing of their own; that they are handed anything is gated where
 * the press is, in the tests of the screens themselves.
 *
 * Recovery is in that last block and used to be in the first, which is the one
 * case where the cost was already being paid: three guest forms print its
 * refusal themselves, and the activation page presses the same endpoint with
 * nothing of its own to print. The flags fixed in the method silenced the
 * interceptor for that press as well, and the page's own `notifyFailure` is
 * silent for those statuses by the same rule - so a 403, a 429 or a 5xx on
 * "отправить еще раз" reached the reader as nothing at all.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("@/shared/api", () => ({
  Api: {
    post: vi.fn().mockResolvedValue({ data: null, error: null }),
    get: vi.fn().mockResolvedValue({ data: null, error: null }),
    delete: vi.fn().mockResolvedValue({ data: null, error: null }),
    patch: vi.fn().mockResolvedValue({ data: null, error: null }),
  },
  X_DM_ACCOUNT_TOKEN: "X-Dm-Account-Token",
}));

import { Api } from "@/shared/api";
import accountApi from "./accountApi";

describe("accountApi.signIn", () => {
  it("takes the refusal over from the response interceptor", async () => {
    await accountApi.signIn({
      email: "reader@dm.am",
      password: "correct horse",
    });

    expect(vi.mocked(Api.post)).toHaveBeenCalledWith(
      "account/login",
      { email: "reader@dm.am", password: "correct horse" },
      { ownsRateLimit: true, ownsRefusal: true },
    );
  });
});

/** As much of a request's options as the rule here is about. */
type Flags = { ownsRefusal?: boolean; ownsRateLimit?: boolean };

/** The options the client was handed for the last request it was given. */
const optionsOfLastCall = () =>
  vi.mocked(Api.post).mock.calls.at(-1)?.[2] as Flags | undefined;

/** The same, for a read: the options are the fourth argument of `Api.get`. */
const optionsOfLastRead = () =>
  vi.mocked(Api.get).mock.calls.at(-1)?.[3] as Flags | undefined;

describe("the guest contour", () => {
  beforeEach(() => {
    vi.mocked(Api.post).mockClear();
  });

  it.each([
    ["регистрация", () => accountApi.register({} as never)],
    ["проверка занятости почты", () => accountApi.checkEmail("reader@dm.am")],
    ["вход", () => accountApi.signIn({} as never)],
    [
      "второй фактор",
      () => accountApi.completeTwoFactorLogin({ code: "123456" }),
    ],
  ])("shows the rate limit itself: %s", async (_, send) => {
    await send();

    expect(optionsOfLastCall()?.ownsRateLimit).toBe(true);
  });

  it.each([
    ["активация", () => accountApi.activate("token", { username: "Lex" })],
    ["выбор имени", () => accountApi.checkUsername("Lex")],
    ["смена пароля", () => accountApi.changePassword({} as never)],
    ["сброс пароля", () => accountApi.completePasswordReset("token", "pass")],
    ["смена почты", () => accountApi.changeEmail({} as never)],
    ["подключение бота", () => accountApi.generateBotCode("telegram")],
  ])("leaves the rate limit to the interceptor: %s", async (_, send) => {
    await send();

    expect(optionsOfLastCall()?.ownsRateLimit).toBeUndefined();
  });
});

/**
 * The requests that leave the decision to whoever sends them.
 *
 * Each is read or pressed from a screen that draws the failure where the reader
 * is looking - a card in place of a block, a strip under a field - and the flag
 * is how that screen keeps the interceptor from writing the same sentence a
 * second time in the corner. What the service may not do is decide it: the
 * state of the second factor is a card in the account block and a discarded
 * read on a profile page, and a flag fixed in the method would silence the
 * plate of the screen that shows nothing at all.
 *
 * So each method is checked twice: that it claims nothing when the call site
 * asked for nothing, and that what the call site asked for reaches the client
 * whole. Which call sites ask is gated where the press is - the account
 * sections, the two removal screens and the three guest forms that send the
 * recovery request have tests of their own; the activation page has one for
 * asking nothing, which is what gave its failures a voice back.
 */
describe("the refusal a caller takes over", () => {
  /** One request, sent with the flags a call site chose, or with none. */
  type Send = (flags?: Flags) => Promise<unknown>;

  const cases: [string, Send, () => Flags | undefined][] = [
    [
      "состояние второго фактора",
      (flags) => accountApi.getTwoFactorStatus(flags),
      optionsOfLastRead,
    ],
    [
      "заявка на смену имени",
      (flags) => accountApi.getUsernameChangeRequest(flags),
      optionsOfLastRead,
    ],
    [
      "назначение снятия второго фактора",
      (flags) => accountApi.scheduleTwoFactorRemoval("a-token", flags),
      optionsOfLastCall,
    ],
    [
      "письмо о снятии второго фактора",
      (flags) => accountApi.requestTwoFactorRemoval("reader@dm.am", flags),
      optionsOfLastCall,
    ],
    [
      "восстановление доступа",
      (flags) => accountApi.recover("reader@dm.am", flags),
      optionsOfLastCall,
    ],
  ];

  beforeEach(() => {
    vi.mocked(Api.get).mockClear();
    vi.mocked(Api.post).mockClear();
  });

  describe.each(cases)("%s", (_, send, optionsOfLast) => {
    it("claims nothing of its own", async () => {
      await send();

      expect(optionsOfLast()?.ownsRefusal).toBeUndefined();
      expect(optionsOfLast()?.ownsRateLimit).toBeUndefined();
    });

    it("carries what the call site set", async () => {
      await send({ ownsRefusal: true, ownsRateLimit: true });

      expect(optionsOfLast()).toMatchObject({
        ownsRefusal: true,
        ownsRateLimit: true,
      });
    });
  });

  /**
   * The one of them whose options were not empty to begin with: the credential
   * of a mailed link travels in a header, and the flags are added beside it.
   * Written as a replacement rather than an addition, the press would go out
   * with no token on it and the server would answer for a link that is not
   * there - which this page draws as a dead link.
   */
  it("keeps the token of the mailed link beside the flags", async () => {
    await accountApi.scheduleTwoFactorRemoval("a-token", { ownsRefusal: true });

    expect(vi.mocked(Api.post).mock.calls.at(-1)?.[2]).toEqual({
      headers: { "X-Dm-Account-Token": "a-token" },
      ownsRefusal: true,
    });
  });
});
