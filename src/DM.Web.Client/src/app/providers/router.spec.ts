/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";
import type { RouteMeta } from "vue-router";
import router from "./router";
import {
  TITLE_SEPARATOR,
  formatDocumentTitle,
  joinTitleSegments,
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
 * Routes retired on purpose, and a route is cheap to bring back by reflex.
 *
 * "Комнаты" was never a page of the site: a game draws a page per room, and
 * rooms are configured in the "Управление комнатами" section of the game
 * settings page, behind that page's master/assistant gate. The flat
 * /game/:id/rooms list drew every room a second time over the sidebar's own
 * room groups, and it asked nothing at all about who was reading it.
 *
 * Name and path both, because either one alone is half the resurrection: a
 * renamed route on the same path is the same duplicate page.
 */
const RETIRED: Array<[string, string]> = [["game-rooms", "/game/abcde/rooms"]];

describe("retired routes", () => {
  it.each(RETIRED)("keeps %s out of the table", (name, path) => {
    const named = router.getRoutes().map((record) => String(record.name));

    expect(named, `${name} is back in the table`).not.toContain(name);
    expect(router.resolve(path).name, `${path} still resolves`).toBe(
      "not-found",
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

/**
 * Zone titles (a game, a blog). Inside a zone the tab has to answer "which
 * game" before "which page of it": the entity name first, the section second,
 * the brand last. And the section is the word the sidebar panel and the page
 * heading already use for that page, never a second name for it.
 *
 * Two shapes are ruled out here. A static `title` on a zone route reads the
 * same in every game and leaves the game name out of the tab altogether. A
 * section shared by a group of routes puts one name on all of them: that is
 * what "Комнаты" did for every room of a game, and the one thing the tab did
 * not say was which room the reader was in.
 */
const HERE = dirname(fileURLToPath(import.meta.url));
// providers -> app -> src
const CLIENT_SRC = resolve(HERE, "..", "..");

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
 * Zone pages that write a title of their own, and what out of. Every other
 * page of a zone is titled by its shell out of `meta.section`; a second writer
 * for one page is two sources for one string, and the winner is whichever
 * effect happens to run last.
 */
const ZONE_TITLE_OWNERS: Record<string, string> = {
  "pages/blog/BlogPage.vue": "blog shell: blog name + meta.section",
  "pages/game/CharacterCreate.vue": "character form: game + ?npc mode",
  "pages/game/GameChatRoom.vue": "chat room: game + room name",
  "pages/game/GamePage.vue": "game shell: game name + meta.section",
  "pages/game/GameRoom.vue": "post room: game + room name",
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

function vueFilesIn(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) vueFilesIn(full, out);
    else if (full.endsWith(".vue")) out.push(full);
  }
  return out;
}

const sourceOf = (file: string) => readFileSync(file, "utf8");

const where = (file: string) =>
  relative(CLIENT_SRC, file).split("\\").join("/");

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
});

describe("room titles", () => {
  const rooms = (findZoneRoot(rawRoutes, "gameZone")?.children ?? []).filter(
    (child) => /rooms\/:num$/.test(child.path),
  );

  it("names a room page by its room, never by the rooms group", () => {
    expect(rooms.length, "a post room and a chat room").toBe(2);
    for (const room of rooms) {
      // A fixed section here would be one tab name for every room of a game.
      expect(room.meta?.section, room.path).toBeUndefined();
      expect(room.meta?.dynamicTitle, room.path).toBe(true);
    }
  });
});

/**
 * Каталог вариантов доступен только в dev-сборке: роут лежит внутри ветки
 * import.meta.env.DEV, которую rollup выбрасывает вместе с динамическим
 * импортом. Проверка живет здесь, потому что страница по правилам FSD не
 * импортирует роутер сама.
 */
describe("the mockup catalogs", () => {
  it("answer at their own routes in a development build", () => {
    const route = router.resolve("/dev/chat-events");

    expect(route.name).toBe("dev-chat-events-variants");
    expect(route.meta.title).toBe("Мокапы: эвенты чата");
  });
});
