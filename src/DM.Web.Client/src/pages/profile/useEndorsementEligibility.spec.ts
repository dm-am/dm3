/**
 * @vitest-environment node
 */

/**
 * The one question behind the "Написать рекомендацию" control and behind the
 * form it opens: may this viewer recommend this user?
 *
 * The create endpoint has five conditions (signed in, not oneself, past
 * probation, played in the same game, none for this pair yet) and only two of
 * them are knowable on the client, so the answer is the server's. What is
 * tested here is that nothing else is ever read as a yes: a refusal, a failed
 * request and an unasked question all leave the control undrawn, and a refusal
 * is repeated in the server's own words rather than paraphrased.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";

vi.mock("@/entities/user", () => ({
  userApi: { getEndorsementEligibility: vi.fn() },
}));

import { userApi } from "@/entities/user";
import type { Username } from "@/shared/api/models/community";
import { useEndorsementEligibility } from "./useEndorsementEligibility";

const target = "Reader" as Username;
/**
 * The endpoint answers an Envelope, as every single-resource read does, so the
 * answer arrives under `resource` and the composable has to unwrap it.
 */
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

  // The envelope is the contract, not decoration: reading the body as if it
  // were the resource leaves canCreate undefined, which is a permanently
  // undrawn control on a profile the viewer may in fact recommend.
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
