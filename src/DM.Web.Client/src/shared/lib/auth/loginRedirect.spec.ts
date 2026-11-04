/**
 * @vitest-environment jsdom
 */

// The destination survives a refusal, and only a path on this site comes back.
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

  // One tuple type: inferred, the cases are a union no callback matches.
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
