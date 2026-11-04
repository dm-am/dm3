/**
 * @vitest-environment node
 */

/**
 * The question behind "Написать рекомендацию": only an explicit yes is a yes,
 * and a refusal is repeated in the server's own words.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";

vi.mock("@/entities/user", () => ({
  userApi: { getEndorsementEligibility: vi.fn() },
}));

import { userApi } from "@/entities/user";
import type { Username } from "@/shared/api/models/community";
import { useEndorsementEligibility } from "./useEndorsementEligibility";

const target = "Reader" as Username;
/** The endpoint answers an Envelope, which the composable unwraps. */
const answer = (resource: unknown) =>
  vi.mocked(userApi.getEndorsementEligibility).mockResolvedValue({
    data: { resource },
    error: null,
  } as never);
const fail = (status: number, title?: string) =>
  vi.mocked(userApi.getEndorsementEligibility).mockResolvedValue({
    data: null,
    error: { status, title: title ?? "" },
  } as never);

describe("useEndorsementEligibility", () => {
  beforeEach(() => vi.mocked(userApi.getEndorsementEligibility).mockReset());

  it("asks the server about the named user", async () => {
    answer({ canCreate: true });
    const { ask } = useEndorsementEligibility();

    await ask(target);

    expect(vi.mocked(userApi.getEndorsementEligibility)).toHaveBeenCalledWith(
      target,
      undefined,
    );
  });

  /** The flags are the consumer's: the profile claims none, the form both. */
  it("passes the consumer's flags through to the request", async () => {
    answer({ canCreate: true });
    const { ask } = useEndorsementEligibility({
      ownsRefusal: true,
      ownsRateLimit: true,
    });

    await ask(target);

    expect(vi.mocked(userApi.getEndorsementEligibility)).toHaveBeenCalledWith(
      target,
      { ownsRefusal: true, ownsRateLimit: true },
    );
  });

  /** A consumer taking the wait over names the seconds, through the shared wording. */
  it("says how long the wait is when the question hit the limit", async () => {
    vi.mocked(userApi.getEndorsementEligibility).mockResolvedValue({
      data: null,
      error: {
        status: 429,
        title: "Слишком много запросов",
        retryAfterSeconds: 30,
      },
    } as never);
    const { canCreate, askError, ask } = useEndorsementEligibility({
      ownsRefusal: true,
      ownsRateLimit: true,
    });

    await ask(target);

    expect(canCreate.value).toBe(false);
    expect(askError.value).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  it("says yes only on the server's explicit yes", async () => {
    answer({ canCreate: true });
    const { canCreate, refusal, ask } = useEndorsementEligibility();

    await ask(target);

    expect(canCreate.value).toBe(true);
    expect(refusal.value).toBe("");
  });

  it("offers nothing before the question is answered", () => {
    const { canCreate } = useEndorsementEligibility();

    expect(canCreate.value).toBe(false);
  });

  // Read as the resource, the body leaves canCreate undefined: a control never drawn.
  it("reads the answer out of the envelope and not off the body", async () => {
    vi.mocked(userApi.getEndorsementEligibility).mockResolvedValue({
      data: { resource: { canCreate: true } },
      error: null,
    } as never);
    const { canCreate, ask } = useEndorsementEligibility();

    await ask(target);

    expect(canCreate.value).toBe(true);
  });

  // The two refusals the write flow exists to respect, in the server's words.
  it.each([
    ["Нельзя рекомендовать самого себя"],
    ["Вы уже рекомендовали этого пользователя"],
  ])("repeats the refusal %s verbatim", async (reason) => {
    answer({ canCreate: false, reason });
    const { canCreate, refusal, ask } = useEndorsementEligibility();

    await ask(target);

    expect(canCreate.value).toBe(false);
    expect(refusal.value).toBe(reason);
  });

  it("treats an unanswered question as no, not as permission", async () => {
    fail(500);
    const { canCreate, askError, ask } = useEndorsementEligibility();

    await ask(target);

    expect(canCreate.value).toBe(false);
    expect(askError.value).toBe(
      "Не удалось проверить, можно ли написать рекомендацию",
    );
  });

  it("drops a yes when the answer is cleared", async () => {
    answer({ canCreate: true });
    const { canCreate, ask, clear } = useEndorsementEligibility();
    await ask(target);

    clear();

    expect(canCreate.value).toBe(false);
  });
});
