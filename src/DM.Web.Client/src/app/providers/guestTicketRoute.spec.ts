/**
 * @vitest-environment jsdom
 */

/**
 * The tracking token is the guest's only credential, so it stands in the
 * fragment, which no server, Referer or proxy sees. The old path address still
 * opens the ticket: it is the only way back, and its logs are written already.
 */
import { describe, it, expect } from "vitest";
import type { RouteLocation, RouteLocationRaw } from "vue-router";
import router from "./router";
import { trackHash, trackToken } from "@/entities/ticket";

const TOKEN = "5eedc0de5eedc0de5eedc0de5eedc0de";

describe("the address of a guest's ticket page", () => {
  it("carries the token in the fragment and nothing in the path", () => {
    const resolved = router.resolve(`/support/track#token=${TOKEN}`);

    expect(resolved.name).toBe("support-track");
    expect(resolved.path).toBe("/support/track");
    expect(resolved.params).toEqual({});
  });

  it("is the address the page and the form build", () => {
    const built = router.resolve({
      name: "support-track",
      hash: trackHash(TOKEN),
    });

    expect(built.href).toBe(`/support/track#token=${TOKEN}`);
    expect(trackToken(built.hash)).toBe(TOKEN);
  });

  it("moves the token of the old address into the fragment", () => {
    const old = router.resolve(`/support/track/${TOKEN}`);
    const record = old.matched.at(-1);

    expect(record?.redirect, "the old address opens nothing").toBeTypeOf(
      "function",
    );

    const redirect = record!.redirect as (
      to: RouteLocation,
    ) => RouteLocationRaw;
    const target = router.resolve(redirect(old));
    expect(target.href).toBe(`/support/track#token=${TOKEN}`);
    expect(trackToken(target.hash)).toBe(TOKEN);
  });

  it("reads no token from a fragment that names none", () => {
    expect(trackToken("")).toBe("");
    expect(trackToken(`#${TOKEN}`)).toBe("");
  });
});
