import { describe, it, expect, vi, beforeEach } from "vitest";

const vote = vi.fn();
const unvote = vi.fn();
const notifyFailure = vi.fn();

vi.mock("@/entities/poll", () => ({
  usePollsStore: () => ({ vote, unvote }),
}));
vi.mock("@/shared/lib/errors", () => ({
  notifyFailure: (...args: unknown[]) => notifyFailure(...args),
}));

const { usePollVote } = await import("./usePollVote");

beforeEach(() => {
  vote.mockReset();
  unvote.mockReset();
  notifyFailure.mockReset();
});

describe("usePollVote", () => {
  it("says a refused cancel of the vote, as it says a refused vote", async () => {
    const error = { status: 409, title: "Опрос уже закрыт" };
    unvote.mockResolvedValue({ error });

    await usePollVote().cancelVote("p-1" as never);

    expect(notifyFailure).toHaveBeenCalledWith(
      error,
      "Не удалось отменить голос",
    );
  });

  it("says nothing when the vote was taken back", async () => {
    unvote.mockResolvedValue({ error: null });

    await usePollVote().cancelVote("p-1" as never);

    expect(notifyFailure).not.toHaveBeenCalled();
  });
});
