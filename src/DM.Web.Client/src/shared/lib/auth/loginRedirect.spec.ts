/**
 * @vitest-environment jsdom
 */

/**
 * The address of a refused navigation, on its way out and on its way back.
 *
 * Two things are pinned here. That the destination survives at all — the guard
 * used to answer with a bare home page, and since it replaces the navigation
 * the address was not in the history either. And that the way back refuses
 * anything that is not a path on this site: the value arrives through the URL,
 * so "redirect" is a field anyone can fill in a link they hand out.
 */
import { describe, expect, it } from "vitest";
import {
  REDIRECT_QUERY_KEY,
  loginLocation,
  resumeTarget,
} from "./loginRedirect";

describe("loginLocation", () => {
  it("remembers the address, query and all", () => {
    expect(loginLocation("/messenger/c/42?number=3")).toEqual({
      name: "home",
      query: {
        action: "login",
        [REDIRECT_QUERY_KEY]: "/messenger/c/42?number=3",
      },
    });
  });

  it("writes nothing down for the home page itself", () => {
    expect(loginLocation("/")).toEqual({
      name: "home",
      query: { action: "login" },
    });
    expect(loginLocation()).toEqual({
      name: "home",
      query: { action: "login" },
    });
  });
});

describe("resumeTarget", () => {
  it("resumes a path on this site", () => {
    expect(resumeTarget("/game/abcde/characters")).toBe(
      "/game/abcde/characters",
    );
  });

  // Annotated as one tuple type: left to inference the cases become a union of
  // three shapes, and a callback that reads only the first element matches none
  // of them. resumeTarget takes unknown, which is the point — these are the
  // values a query string can actually deliver.
  it.each<[unknown, string]>([
    ["//evil.example", "protocol-relative"],
    ["/\\evil.example", "a backslash browsers normalise"],
    ["https://evil.example", "an absolute address"],
    ["game/abcde", "a relative path"],
    [undefined, "nothing at all"],
    [["/a", "/b"], "a repeated query key"],
  ])("refuses %s (%s)", (value) => {
    expect(resumeTarget(value)).toBeNull();
  });
});
