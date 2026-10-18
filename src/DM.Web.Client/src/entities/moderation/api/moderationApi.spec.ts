/**
 * @vitest-environment node
 */

/**
 * The one call on this client that changes a warning.
 *
 * A warning used to be deleted, and the client asked for that with DELETE on
 * the same address this now PATCHes. The server has no delete any more, so a
 * mirror left on the old verb would answer 405 with a form full of words the
 * moderator had just written — and nothing in the type system can see it,
 * because the verb and the address are strings.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";

const patch = vi.hoisted(() => vi.fn());
const get = vi.hoisted(() => vi.fn());
const post = vi.hoisted(() => vi.fn());
const remove = vi.hoisted(() => vi.fn());

vi.mock("@/shared/api", () => ({
  Api: {
    get,
    post,
    put: vi.fn(),
    patch,
    delete: remove,
  },
}));

const { default: moderationApi } = await import("./moderationApi");

describe("the moderation client", () => {
  beforeEach(() => patch.mockReset());

  it("corrects the points of a warning in place", () => {
    moderationApi.changeWarningPoints(
      "w-1",
      { points: 0, reason: "Ошибка" },
      { ownsRefusal: true, ownsRateLimit: true },
    );

    // The flags are the window's, which prints a refusal in itself: dropped on
    // the way, the interceptor would say it a second time in the corner.
    expect(patch).toHaveBeenCalledWith(
      "moderation/warnings/w-1",
      { points: 0, reason: "Ошибка" },
      { ownsRefusal: true, ownsRateLimit: true },
    );
  });
});

/**
 * The two doors of a removed account, which share one address.
 *
 * Taking an account and giving it back differ by the verb alone — DELETE and
 * POST on moderation/users/{name}/account — so a client that spelled the wrong
 * one would be asking for the opposite act, and be answered by a 405 nothing
 * here can see: the verb and the address are both strings.
 *
 * The name travels inside the address, so it is escaped there. A name carrying
 * a slash or a question mark would otherwise leave the path it belongs to and
 * land on an address nobody serves.
 */
describe("the removed accounts", () => {
  beforeEach(() => {
    get.mockReset();
    post.mockReset();
    remove.mockReset();
  });

  it("gives an account back by posting to the address the removal deletes", () => {
    moderationApi.restoreAccount("ivan");

    expect(post).toHaveBeenCalledWith(
      "moderation/users/ivan/account",
      undefined,
      undefined,
    );
    expect(remove).not.toHaveBeenCalled();
  });

  it("escapes the name it puts in the address", () => {
    moderationApi.restoreAccount("a/b?c");

    expect(post.mock.calls[0][0]).toBe("moderation/users/a%2Fb%3Fc/account");
  });

  /**
   * The window that asks before an account comes back prints a refusal in
   * itself, so the flags that keep the interceptor quiet ride on the request.
   */
  it("carries the flags of a window that shows the refusal itself", () => {
    const flags = { ownsRefusal: true, ownsRateLimit: true };
    moderationApi.restoreAccount("ivan", flags);

    expect(post).toHaveBeenCalledWith(
      "moderation/users/ivan/account",
      undefined,
      flags,
    );
  });

  it("asks for the removed accounts with the search term and the page", () => {
    moderationApi.getRemovedAccounts({ search: "ivan", skip: 25, take: 25 });

    expect(get).toHaveBeenCalledWith("moderation/users/removed", {
      search: "ivan",
      skip: 25,
      take: 25,
    });
  });

  it("leaves the term out when nothing narrows the list", () => {
    moderationApi.getRemovedAccounts({ skip: 0, take: 25 });

    expect(get).toHaveBeenCalledWith("moderation/users/removed", {
      search: undefined,
      skip: 0,
      take: 25,
    });
  });
});

/**
 * The lift of a ban carries its reason in the body of the DELETE, the way the
 * removal of an account carries its own: the server refuses a lift without one,
 * and a reason put in the address would end up in logs and referrers.
 */
describe("the lift of a ban", () => {
  beforeEach(() => remove.mockReset());

  it("sends the reason in the body, with the window's flags", () => {
    moderationApi.liftBan("b-1", "Разобрались", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });

    expect(remove).toHaveBeenCalledWith(
      "bans/b-1",
      { reason: "Разобрались" },
      { ownsRefusal: true, ownsRateLimit: true },
    );
  });
});

/**
 * The ban list is narrowed by name the way the warning log is, through the
 * address of the request: the profile of a person leads to his bans by it.
 */
describe("the ban list", () => {
  beforeEach(() => get.mockReset());

  it("asks for the bans of the name it is given, a page at a time", () => {
    moderationApi.getBanHistory({ username: "SolohinLex", skip: 20, take: 20 });

    expect(get).toHaveBeenCalledWith("bans/history", {
      username: "SolohinLex",
      skip: 20,
      take: 20,
    });
  });

  it("leaves the name out when nothing narrows the list", () => {
    moderationApi.getBanHistory({ skip: 0, take: 20 });

    expect(get).toHaveBeenCalledWith("bans/history", {
      username: undefined,
      skip: 0,
      take: 20,
    });
  });
});

/**
 * The picture of a profile a warning was issued on comes out of the record by
 * a DELETE of its own, with the flags of the window that asks first: the
 * window prints a refusal in itself.
 */
describe("the picture of a warning on a profile", () => {
  beforeEach(() => remove.mockReset());

  it("is taken out of the record at the address of the record", () => {
    moderationApi.removeWarningProfilePicture("w-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });

    expect(remove).toHaveBeenCalledWith(
      "moderation/warnings/w-1/profile-picture",
      undefined,
      { ownsRefusal: true, ownsRateLimit: true },
    );
  });
});
