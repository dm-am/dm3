/**
 * @vitest-environment node
 */

import { describe, it, expect, vi } from "vitest";

const post = vi.hoisted(() => vi.fn());

vi.mock("@/shared/api", () => ({
  Api: { post },
}));

const { default: moderationApi } = await import("./moderationApi");

describe("the removed accounts", () => {
  // A slash or a question mark would otherwise leave the path it belongs to.
  it("escapes the name it puts in the address", () => {
    moderationApi.restoreAccount("a/b?c");

    expect(post.mock.calls[0][0]).toBe("moderation/users/a%2Fb%3Fc/account");
  });
});
