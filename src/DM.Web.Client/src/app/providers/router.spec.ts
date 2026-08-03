/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import type { RouteMeta } from "vue-router";
import router from "./router";
import {
  TITLE_SEPARATOR,
  formatDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";

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
    expect(new Set(named).size, "route names must be unique").toBe(
      named.length,
    );
  });
});

/**
 * Document titles. A route that says nothing about its title is not neutral:
 * `afterEach` writes the bare brand over whatever stood there, so the tab of a
 * game, of a blog, of a resolver page reads "Dungeon Master" and keeps reading
 * that while the reader walks the whole zone. Twenty two named routes were in
 * that state at once and nothing in the suite noticed.
 *
 * One source per route, declared in the route's OWN meta: `title` (static),
 * `section` (a zone sub-route, the shell composes it with the entity name) or
 * `dynamicTitle` (the component chain builds it once data arrives). Own meta,
 * because a title inherited from a parent record is one tab name shared by
 * every child — eight moderation pages and three messenger pages were exactly
 * that.
 */
const TITLE_MARKERS = ["title", "section", "dynamicTitle"] as const;

const markersOf = (meta: RouteMeta): string[] =>
  TITLE_MARKERS.filter((marker) => meta[marker] !== undefined);

/** The brand the formatter appends — no title may carry it itself. */
const BRAND = formatDocumentTitle("");

describe("document titles", () => {
  const records = router.getRoutes();

  it("declares exactly one title source on every named route", () => {
    const offenders = records
      .filter((record) => record.name)
      .map((record) => ({
        name: String(record.name),
        markers: markersOf(record.meta),
      }))
      .filter(({ markers }) => markers.length !== 1)
      .map(({ name, markers }) =>
        markers.length === 0
          ? `${name}: no title source`
          : `${name}: ${markers.join(" + ")}`,
      );

    expect(offenders).toEqual([]);
  });

  it("keeps title sources off parent records", () => {
    // `getRoutes()` returns each record's own meta (the merge into
    // `resolve().meta` happens later), so this is the check inheritance
    // cannot slip past.
    const offenders = records
      .filter((record) => record.children.length > 0)
      .filter((record) => markersOf(record.meta).length > 0)
      .map((record) => `${record.path}: ${markersOf(record.meta).join(" + ")}`);

    expect(offenders).toEqual([]);
  });

  it("keeps every title string inside the interface copy rules", () => {
    // The signs ruled out of interface copy, plus the two the formatter owns:
    // the separator and the brand are appended, never written by hand.
    const forbidden: Array<[string, string]> = [
      ["—", "em dash"],
      [";", "semicolon"],
      ["\u0451", "the letter yo"],
      [TITLE_SEPARATOR, "a hand-glued separator"],
      [BRAND, "the brand"],
    ];

    const offenders: string[] = [];
    for (const record of records) {
      for (const key of ["title", "section"] as const) {
        const value = record.meta[key];
        if (value === undefined) continue;
        const where = `${String(record.name ?? record.path)}.${key}`;
        if (!value.trim()) {
          offenders.push(`${where}: empty`);
          continue;
        }
        for (const [sign, what] of forbidden) {
          if (value.includes(sign)) {
            offenders.push(`${where} "${value}": ${what}`);
          }
        }
      }
    }

    expect(offenders).toEqual([]);
  });
});
