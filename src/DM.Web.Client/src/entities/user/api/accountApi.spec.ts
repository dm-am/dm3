/**
 * @vitest-environment jsdom
 */

// The flags are the joint no test of a form can see: its transport is mocked out.
// The guest forms claim theirs in the method; every other method carries what it is handed.
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
    ["the registration", () => accountApi.register({} as never)],
    ["the check of an address", () => accountApi.checkEmail("reader@dm.am")],
    ["the sign-in", () => accountApi.signIn({} as never)],
    [
      "the second factor",
      () => accountApi.completeTwoFactorLogin({ code: "123456" }),
    ],
  ])("shows the rate limit itself: %s", async (_, send) => {
    await send();

    expect(optionsOfLastCall()?.ownsRateLimit).toBe(true);
  });

  it.each([
    ["the activation", () => accountApi.activate("token", { username: "Lex" })],
    ["the choice of a name", () => accountApi.checkUsername("Lex")],
    [
      "the change of the password",
      () => accountApi.changePassword({} as never),
    ],
    [
      "the reset of the password",
      () => accountApi.completePasswordReset("token", "pass"),
    ],
    ["the change of the address", () => accountApi.changeEmail({} as never)],
    ["the link of a bot", () => accountApi.generateBotCode("telegram")],
  ])("leaves the rate limit to the interceptor: %s", async (_, send) => {
    await send();

    expect(optionsOfLastCall()?.ownsRateLimit).toBeUndefined();
  });
});

// A screen that draws the failure itself sets the flags; the method claims none of its own.
describe("the refusal a caller takes over", () => {
  /** One request, sent with the flags a call site chose, or with none. */
  type Send = (flags?: Flags) => Promise<unknown>;

  const cases: [string, Send, () => Flags | undefined][] = [
    [
      "the state of the second factor",
      (flags) => accountApi.getTwoFactorStatus(flags),
      optionsOfLastRead,
    ],
    [
      "the request to change the name",
      (flags) => accountApi.getUsernameChangeRequest(flags),
      optionsOfLastRead,
    ],
    [
      "the scheduled removal of the second factor",
      (flags) => accountApi.scheduleTwoFactorRemoval("a-token", flags),
      optionsOfLastCall,
    ],
    [
      "the letter about the removal of the second factor",
      (flags) => accountApi.requestTwoFactorRemoval("reader@dm.am", flags),
      optionsOfLastCall,
    ],
    [
      "the recovery of access",
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

  // The token of a mailed link travels in a header, and the flags are added beside it.
  it("keeps the token of the mailed link beside the flags", async () => {
    await accountApi.scheduleTwoFactorRemoval("a-token", { ownsRefusal: true });

    expect(vi.mocked(Api.post).mock.calls.at(-1)?.[2]).toEqual({
      headers: { "X-Dm-Account-Token": "a-token" },
      ownsRefusal: true,
    });
  });
});
