/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import type { RouteMeta } from "vue-router";

import router from "./router";
import {
  TITLE_SEPARATOR,
  formatDocumentTitle,
  joinTitleSegments,
} from "@/shared/lib/composables/useDocumentTitle";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/**
 * One title source per named route, in its own meta: an inherited title names
 * every child alike, and a route with none gets the bare brand.
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
    // getRoutes() returns each record's own meta, before the merge.
    const offenders = records
      .filter((record) => record.children.length > 0)
      .filter((record) => markersOf(record.meta).length > 0)
      .map((record) => `${record.path}: ${markersOf(record.meta).join(" + ")}`);

    expect(offenders).toEqual([]);
  });

  it("keeps every title string inside the interface copy rules", () => {
    // The separator and the brand are appended, never written by hand.
    const forbidden: Array<[string, string]> = [
      ["—", "em dash"],
      [";", "semicolon"],

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

/**
 * Zone titles: the entity first, then the section, then the brand; no static
 * title and no section shared by a group of routes.
 */

/** A stand-in entity name, to compose a zone title without a live store. */
const ENTITY = "Хроники Амбера";

interface RawRoute {
  path: string;
  meta?: RouteMeta;
  children?: RawRoute[];
}

const ZONES = [
  {
    what: "game",
    flag: "gameZone",
    dir: "pages/game",
    /** The composition every title of the zone has to open with. */
    entity: /joinTitleSegments\(\s*game\.value\?\.title/,
  },
  {
    what: "blog",
    flag: "blogZone",
    dir: "pages/blog",
    entity: /joinTitleSegments\(\s*blog\.value\?\.title/,
  },
] as const;

/**
 * The only writers of a zone title: a page whose section is data announces it
 * through useZoneSection, or two effects race for one string.
 */
const ZONE_TITLE_OWNERS: Record<string, string> = {
  "pages/blog/BlogPage.vue": "blog shell: blog name + section",
  "pages/game/GamePage.vue": "game shell: game name + section",
};

const rawRoutes = router.options.routes as unknown as RawRoute[];

function findZoneRoot(
  routes: readonly RawRoute[],
  flag: "gameZone" | "blogZone",
): RawRoute | null {
  for (const route of routes) {
    if (route.meta?.[flag]) return route;
    const nested = route.children ? findZoneRoot(route.children, flag) : null;
    if (nested) return nested;
  }
  return null;
}

const vueFilesIn = (dir: string) =>
  collect(dir, (name) => name.endsWith(".vue"));

const sourceOf = (file: string) => readFileSync(file, "utf8");

const where = asPath;

for (const zone of ZONES) {
  describe(`${zone.what} zone titles`, () => {
    const root = findZoneRoot(rawRoutes, zone.flag);
    const children = root?.children ?? [];
    const sections = children
      .map((child) => child.meta?.section)
      .filter((section): section is string => section !== undefined);

    it("keeps the shell and its sub-routes in place", () => {
      expect(root, `no record carries ${zone.flag}`).not.toBeNull();
      expect(children.length).toBeGreaterThan(5);
      expect(sections.length).toBeGreaterThan(3);
    });

    it("never titles a route of the zone statically", () => {
      const offenders = children
        .filter((child) => child.meta?.title !== undefined)
        .map((child) => `${child.path}: ${child.meta?.title}`);

      expect(offenders).toEqual([]);
    });

    it("composes the entity, the section and the brand, each once", () => {
      const offenders: string[] = [];
      for (const section of sections) {
        const title = formatDocumentTitle(joinTitleSegments(ENTITY, section));
        const segments = title.split(TITLE_SEPARATOR);
        const repeats = title.split(section).length - 1;
        if (segments.length !== 3 || segments[0] !== ENTITY || repeats !== 1) {
          offenders.push(title);
        }
      }

      expect(offenders).toEqual([]);
    });

    it("never lets a group of routes share one section", () => {
      const shared = sections.filter(
        (section, index) => sections.indexOf(section) !== index,
      );

      expect(shared).toEqual([]);
    });
  });
}

describe("zone pages that title themselves", () => {
  const writers = ZONES.flatMap((zone) =>
    vueFilesIn(join(CLIENT_SRC, zone.dir)).map((file) => ({ zone, file })),
  ).filter(({ file }) => sourceOf(file).includes("useDocumentTitle("));

  it("is the shells and the data-titled pages, and nothing else", () => {
    expect(writers.map(({ file }) => where(file)).sort()).toEqual(
      Object.keys(ZONE_TITLE_OWNERS).sort(),
    );
  });

  it("opens every one of them with the entity name", () => {
    const offenders = writers
      .filter(({ zone, file }) => !zone.entity.test(sourceOf(file)))
      .map(({ file }) => where(file));

    expect(offenders).toEqual([]);
  });

  it("draws no second heading on a page the shell already names", () => {
    // The shell renders the one h1 of the zone; the index of each zone is
    // mounted outside the shell and owns its heading.
    const outsideTheShell = new Set([
      "pages/game/GamesPage.vue",
      "pages/blog/BlogsPage.vue",
    ]);

    const offenders = ZONES.flatMap((zone) =>
      vueFilesIn(join(CLIENT_SRC, zone.dir)),
    )
      .filter((file) => !outsideTheShell.has(where(file)))
      .filter((file) => !(where(file) in ZONE_TITLE_OWNERS))
      .filter((file) => /<page-title|<PageTitle/.test(sourceOf(file)))
      .map((file) => where(file))
      .sort();

    expect(offenders).toEqual([]);
  });
});

describe("room titles", () => {
  // The room of posts is a tree of its own, outside the zone shell.
  const rooms = router
    .getRoutes()
    .filter((record) => /rooms\/:num$/.test(record.path) && record.name);

  it("names a room page by its room, never by the rooms group", () => {
    expect(rooms.length, "a post room and a chat room").toBe(2);
    for (const room of rooms) {
      // A fixed section here would be one tab name for every room of a game.
      expect(room.meta?.section, room.path).toBeUndefined();
      expect(room.meta?.dynamicTitle, room.path).toBe(true);
    }
  });
});

// afterEach writes the root route's title over the static head on boot.
describe("the root route and the static head", () => {
  const indexHtml = readFileSync(join(CLIENT_SRC, "..", "index.html"), "utf8");
  const ogTitle = /<meta property="og:title" content="([^"]*)"/.exec(
    indexHtml,
  )?.[1];
  const staticTitle = /<title>([\s\S]*?)<\/title>/.exec(indexHtml)?.[1].trim();

  it("name the site with one string", () => {
    const rootTitle = router.resolve("/").meta.title;

    expect(ogTitle, "index.html declares no og:title").toBeTruthy();
    expect(rootTitle).toBe(ogTitle);
    expect(staticTitle).toBe(formatDocumentTitle(rootTitle));
  });
});

// While an error page shows, the title comes from the error: the param names nobody.
describe("titles while an error page is showing", () => {
  const owners = vueFilesIn(join(CLIENT_SRC, "pages")).filter((file) => {
    const source = sourceOf(file);
    return (
      source.includes("<ErrorPage") && source.includes("useDocumentTitle(")
    );
  });

  it("finds the pages that both title themselves and draw an error", () => {
    expect(owners.length).toBeGreaterThan(1);
  });

  it("takes the title from the error and not from the route param", () => {
    const offenders = owners
      .filter((file) => !sourceOf(file).includes("getErrorConfig("))
      .map(where);

    expect(offenders).toEqual([]);
  });
});
