/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import router from "./router";

/**
 * The shell — left and right sidebars — is what every page is mounted inside,
 * and a route that forgets it renders a bare page with no navigation. This
 * asserts the shell and the zone flags for a sample spanning every zone, so the
 * route table can be restructured without silently dropping either.
 */
const SHELLED: Array<[string, Record<string, unknown>]> = [
  ["/", { title: "Главная страница" }],
  ["/about", {}],
  ["/forum", {}],
  ["/games", {}],
  ["/blogs", {}],
  ["/users/somebody", {}],
  ["/game/abcde", { gameZone: true }],
  ["/blogs/abcde", { blogZone: true }],
  ["/moderation", { moderationZone: true, requiresAuth: true }],
  ["/messenger", { requiresAuth: true }],
  ["/chat", {}],
  ["/no-such-page-at-all", {}],
];

function shellOf(path: string) {
  const resolved = router.resolve(path);
  const components = resolved.matched.flatMap((record) =>
    Object.keys(record.components ?? {}),
  );
  return {
    name: resolved.name,
    hasLeft: components.includes("left"),
    hasRight: components.includes("right"),
    hasPage: components.includes("page") || components.includes("default"),
    meta: resolved.meta,
  };
}

describe("route table", () => {
  it.each(SHELLED)("mounts %s inside the shell", (path, meta) => {
    const shell = shellOf(path);

    expect(shell.hasLeft, `${path} has no left sidebar`).toBe(true);
    expect(shell.hasRight, `${path} has no right sidebar`).toBe(true);
    expect(shell.hasPage, `${path} resolves to no page`).toBe(true);
    expect(shell.meta).toMatchObject(meta);
  });

  it("leaves the OAuth callback outside the shell on purpose", () => {
    const shell = shellOf("/auth/callback");

    expect(shell.name).toBe("auth-callback");
    expect(shell.hasLeft).toBe(false);
    expect(shell.hasRight).toBe(false);
  });

  it("resolves every declared name to a path", () => {
    const named = router
      .getRoutes()
      .filter((record) => record.name)
      .map((record) => record.name as string);

    expect(named.length).toBeGreaterThan(50);
    expect(new Set(named).size, "route names must be unique").toBe(named.length);
  });
});
