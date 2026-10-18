/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, vi } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";
import { createPinia, setActivePinia } from "pinia";
import type {
  RouteLocationNormalized,
  RouteLocationRaw,
  RouteMeta,
} from "vue-router";
/**
 * The question the guard waits on when the store holds nobody: who the session
 * cookie belongs to. Answered per test; a guest by default.
 */
const whoAmI = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/api/personalApi", () => ({
  default: { getMyProfile: whoAmI },
}));

import router, { guardAuthenticated } from "./router";
import { REDIRECT_QUERY_KEY } from "@/shared/lib/auth";
import {
  TITLE_SEPARATOR,
  formatDocumentTitle,
  joinTitleSegments,
} from "@/shared/lib/composables/useDocumentTitle";
import {
  MODERATION_SECTIONS,
  moderationPageRole,
} from "@/shared/config/moderationSections";

/**
 * The shell — left and right sidebars — is what every page is mounted inside,
 * and a route that forgets it renders a bare page with no navigation. This
 * asserts the shell and the zone flags for a sample spanning every zone, so the
 * route table can be restructured without silently dropping either.
 */
const SHELLED: Array<[string, Record<string, unknown>]> = [
  ["/", { title: "Форумные ролевые игры" }],
  ["/about", {}],
  ["/forum", {}],
  ["/games", {}],
  ["/blogs", {}],
  ["/users/somebody", {}],
  ["/game/abcde", { gameZone: true }],
  ["/blogs/abcde", { blogZone: true }],
  ["/moderation", { moderationZone: true, requiresAuth: true }],
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

  /**
   * The messenger lays its list of conversations where the right column
   * stands, so it is mounted with the left column and without the right one,
   * every page of it, and every page of it needs a session.
   */
  it.each([
    ["/messenger", "messenger"],
    ["/messenger/c/c1", "chat"],
    ["/messenger/user/somebody", "direct-message"],
  ])("mounts %s without the right column", (path, name) => {
    const shell = shellOf(path);
    expect(shell.name).toBe(name);
    expect(shell.hasLeft).toBe(true);
    expect(shell.hasRight).toBe(false);
    expect(shell.hasPage).toBe(true);
    expect(shell.meta).toMatchObject({ requiresAuth: true });
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

  /**
   * Two kinds of review, four subpages, and names that read alike.
   *
   * received-reviews and given-reviews are ratings of single posts and were
   * taken long before the game pair existed; the profile counters for reviews
   * of whole games therefore have their own names, and pointing either counter
   * at the older pair would open a page about the other kind of review with no
   * error to notice.
   */
  it.each([
    ["received-game-reviews", "/users/somebody/received-game-reviews"],
    ["given-game-reviews", "/users/somebody/given-game-reviews"],
  ])("gives %s a route of its own", (name, path) => {
    const shell = shellOf(path);

    expect(shell.name, `${path} does not resolve to ${name}`).toBe(name);
    expect(shell.hasLeft).toBe(true);
    expect(shell.hasRight).toBe(true);
    expect(shell.hasPage).toBe(true);
    expect(shell.meta).toMatchObject({ dynamicTitle: true });

    const postReviews = router.resolve(
      path.replace("-game-reviews", "-reviews"),
    );
    expect(
      postReviews.name,
      "the game pair must not resolve to the post pair",
    ).not.toBe(name);
  });

  /**
   * The room of posts is read across the whole middle, as on the old site: the
   * left column with the menu of the game, and no right one. Its tree keeps
   * the game shell at the depth the game's other pages have it, so moving
   * between the room and the rest of the game keeps the shell mounted.
   */
  it("mounts the room of posts without the right column, in the game shell", () => {
    const room = shellOf("/game/abcde/rooms/3");
    expect(room.name).toBe("game-room");
    expect(room.hasLeft).toBe(true);
    expect(room.hasRight).toBe(false);
    expect(room.hasPage).toBe(true);
    expect(room.meta).toMatchObject({ gameZone: true, dynamicTitle: true });

    const resolved = router.resolve("/game/abcde/rooms/3");
    expect(resolved.params).toMatchObject({ id: "abcde", num: "3" });
    const game = router.resolve("/game/abcde");
    expect(
      resolved.matched.map((record) => Object.keys(record.components ?? {})),
    ).toEqual([["left", "page"], ["default"], ["default"]]);
    expect(game.matched[1].components?.default).toBeDefined();

    // The game itself, its chat rooms and its other pages keep both columns.
    expect(shellOf("/game/abcde/chat-rooms/2").hasRight).toBe(true);
    expect(game.name).toBe("game");
    expect(shellOf("/game/abcde").hasRight).toBe(true);
    expect(
      router.resolve({ name: "game-room", params: { id: "abcde", num: 3 } })
        .path,
    ).toBe("/game/abcde/rooms/3");
  });
});

/**
 * The guard between a guest and a page that needs a session.
 *
 * It used to answer with the bare home page: the address the viewer had asked
 * for was dropped, and because the guard REPLACES the navigation that address
 * never reached the history either. Someone who followed a link to a
 * conversation, signed in, and looked around found themselves on the front page
 * with the link gone.
 */
describe("the guest guard", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
    whoAmI.mockReset();
    whoAmI.mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 401, traceId: "" },
    });
  });

  // resolve() answers a resolved location, which carries an href the guard's
  // parameter type does not declare. The guard reads meta and fullPath, both of
  // which a resolved location has; resolving a real route is what makes these
  // assertions about the route table rather than about a hand-built object.
  const arriving = (path: string) =>
    router.resolve(path) as unknown as RouteLocationNormalized;

  it("carries the address the guest was refused", async () => {
    const decision = (await guardAuthenticated(
      arriving("/messenger/c/42?number=3"),
    )) as {
      name: string;
      query: Record<string, string>;
    };

    expect(decision.name).toBe("home");
    expect(decision.query.action).toBe("login");
    // fullPath and not the route name: the query is part of the address.
    expect(decision.query[REDIRECT_QUERY_KEY]).toBe("/messenger/c/42?number=3");
  });

  it("lets a signed-in viewer through", async () => {
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
    setActivePinia(createPinia());

    expect(await guardAuthenticated(arriving("/messenger"))).toBeUndefined();
  });

  it("lets anyone through a page that asks for nothing", async () => {
    expect(await guardAuthenticated(arriving("/forum"))).toBeUndefined();
    expect(whoAmI).not.toHaveBeenCalled();
  });

  // The browser holds nobody and the cookie holds a session: a link to a
  // protected page opens the page, instead of the login window under a header
  // that greets the reader a moment later.
  it("waits for the session the server still holds", async () => {
    whoAmI.mockResolvedValue({
      data: { id: "user-1", username: "SolohinLex" },
      error: null,
    });

    expect(await guardAuthenticated(arriving("/messenger"))).toBeUndefined();
    expect(whoAmI).toHaveBeenCalledTimes(1);
  });

  it("asks a guest once, however many protected pages he opens", async () => {
    await guardAuthenticated(arriving("/messenger"));
    const second = (await guardAuthenticated(arriving("/notifications"))) as {
      query: Record<string, string>;
    };

    expect(second.query.action).toBe("login");
    expect(whoAmI).toHaveBeenCalledTimes(1);
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
 * "Возврат от внешней авторизации" was the other one, and it was the only
 * record that lived outside the shell: no provider redirected to it because the
 * server has no external sign-in, so the page said "OAuth не настроен" to
 * whoever typed the address and bounced them home three seconds later. The
 * route is also why the shell record carries the comment it does, and bringing
 * it back would bring back a bare page nothing leads to.
 *
 * Name and path both, because either one alone is half the resurrection: a
 * renamed route on the same path is the same duplicate page.
 */
const RETIRED: Array<[string, string]> = [
  ["game-rooms", "/game/abcde/rooms"],
  ["auth-callback", "/auth/callback"],
];

/**
 * /removed-account was the page every "Аккаунт удален" signature led to, and
 * it only explained the name. The signatures lead to the profile of the name
 * now, which gathers what removed accounts left, and a link to the old
 * address, bookmarked or copied, lands on that profile instead of on "не
 * найдено". The redirect is resolved the way the router follows it: the
 * record's own target, handed to resolve.
 */
describe("the old address of a removed account", () => {
  it("redirects to the profile of the name", () => {
    const record = router
      .getRoutes()
      .find((one) => one.path === "/removed-account");

    expect(record, "/removed-account is gone from the table").toBeTruthy();
    expect(record!.redirect, "/removed-account is a page again").toBeTruthy();
    expect(record!.components ?? {}).toEqual({});

    const target = router.resolve(record!.redirect as RouteLocationRaw);
    expect(target.name).toBe("profile");
    expect(target.params).toEqual({ username: "Аккаунт удален" });
  });
});

/**
 * One ticket of the reader's own. The support form leads its signed-in author
 * here after the press and the notification of the answer leads here later
 * (the dispatcher builds the same /my-tickets/{id}), so the address has to be a
 * page of the shell, and one a guest is turned away from: the ticket is read
 * through an endpoint that answers only its author and moderation.
 */
describe("the page of one of my tickets", () => {
  it("opens at the address the notification of the answer leads to", () => {
    const shell = shellOf("/my-tickets/3f2a9c4e-0000-0000-0000-000000000001");

    expect(shell.name).toBe("my-ticket");
    expect(shell.hasLeft).toBe(true);
    expect(shell.hasRight).toBe(true);
    expect(shell.hasPage).toBe(true);
    expect(shell.meta).toMatchObject({ requiresAuth: true });
  });

  it("leaves the list of them where it was", () => {
    expect(router.resolve("/my-tickets").name).toBe("my-tickets");
  });
});

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
 * The only pages of a zone that write a title: its two shells.
 *
 * The heading on the page and the name of the tab are one sentence — "{entity}
 * | {section}" — and the shell composes it. A sub-page whose section is data (a
 * room, a character, an NPC sheet) announces the section through
 * `useZoneSection` rather than writing a title of its own: a second writer for
 * one string means the winner is whichever effect happens to run last, and the
 * page ends up with two h1's saying different things.
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

  it("draws no second heading on a page the shell already names", () => {
    // The shell renders the one h1 of the zone. A sub-page that renders its own
    // puts two on the page, and the second one repeats a word the first says or
    // contradicts it — "Настройки игры" under a heading that said only the game.
    //
    // The index of each zone lives in the same folder and is mounted OUTSIDE
    // the shell — /games and /blogs have no game and no blog behind them — so
    // it owns its heading like any ordinary page.
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
  // Read off the whole table: the room of posts is a tree of its own, outside
  // the shell that holds the rest of the zone (it has no right column).
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

/**
 * The character routes of a game share one prefix, and one of them takes the id
 * of a character as a parameter. "characters/create" is a literal segment and
 * outranks the parameter in the router's own scoring, but the page that would
 * be lost to a mistake here is the one a player opens to join a game.
 */
describe("character routes of a game", () => {
  const CASES: Array<[string, string]> = [
    ["/game/abcde/characters", "game-characters"],
    ["/game/abcde/characters/create", "game-character-create"],
    ["/game/abcde/characters/7f1c/edit", "game-character-edit"],
    ["/game/abcde/characters/7f1c", "game-character"],
  ];

  it.each(CASES)("resolves %s to %s", (path, name) => {
    expect(router.resolve(path).name).toBe(name);
  });
});

/**
 * The feed of a blog and the publications under it, the same prefix problem one
 * zone over: "feed/create" is a literal segment competing with "feed/:pubId",
 * and the page lost to a mistake here is the one the author writes in.
 *
 * The publication page itself is what the feed cards, the profile spotlight and
 * the notification resolver all point at, so its name is load-bearing in three
 * places that cannot see each other.
 */
describe("publication routes of a blog", () => {
  const CASES: Array<[string, string]> = [
    ["/blogs/abcde/feed", "blog-feed"],
    ["/blogs/abcde/feed/create", "blog-publication-create"],
    ["/blogs/abcde/feed/7f1c", "blog-publication"],
    ["/blogs/abcde/feed/7f1c/edit", "blog-publication-edit"],
    ["/publication/7f1c", "publication-redirect"],
  ];

  it.each(CASES)("resolves %s to %s", (path, name) => {
    expect(router.resolve(path).name).toBe(name);
  });

  it("opens the publication page to a guest", () => {
    expect(router.resolve("/blogs/abcde/feed/7f1c").meta).toMatchObject({
      blogZone: true,
    });
    expect(
      router.resolve("/blogs/abcde/feed/7f1c").meta.requiresAuth,
    ).toBeUndefined();
  });
});

/**
 * An address of a route record, its parameters filled with a stand-in value:
 * what a link to that page would carry.
 */
const addressOf = (path: string) => path.replace(/:\w+/g, "x");

/**
 * The moderation zone: one record owns it, and every section is a child of it.
 *
 * The owner is what sends a guest to sign in, what the left sidebar mounts the
 * moderation menu by, and the door that answers a viewer below a section's
 * role with the 403 page instead of a refusal line under somebody else's
 * heading. A section declared beside the zone instead of inside it would have
 * none of the three, and would look like it worked to whoever tried it as an
 * administrator.
 */
describe("the moderation zone", () => {
  const zoneRoutes = router
    .getRoutes()
    .filter((record) => record.path.startsWith("/moderation/"));

  // The section every moderator may open, so the zone's own address never
  // answers a moderator with the 403 page.
  it("opens on the moderators, a section open to every moderator", () => {
    const entry = router
      .getRoutes()
      .find((record) => record.path === "/moderation" && record.redirect);

    expect(entry, "/moderation is not a redirect").toBeTruthy();
    const target = router.resolve(entry!.redirect as RouteLocationRaw);
    expect(target.name).toBe("moderation-moderators");
    expect(moderationPageRole(String(target.name))).toBe("Moderator");
  });

  it("has no page of its own at its address", () => {
    // The overview said "Выберите раздел модерации" beside the menu it meant.
    const pages = router
      .getRoutes()
      .filter((record) => record.path === "/moderation" && record.name);

    expect(pages).toEqual([]);
  });

  it("holds every page under /moderation", () => {
    expect(zoneRoutes.length).toBeGreaterThan(15);

    const outside = zoneRoutes
      .map((record) => record.path)
      .filter((path) => {
        const resolved = router.resolve(addressOf(path));
        const insideTheZone = resolved.matched.some(
          (record) => record.path === "/moderation" && record.children.length,
        );
        return (
          !insideTheZone ||
          resolved.meta.moderationZone !== true ||
          resolved.meta.requiresAuth !== true
        );
      });

    expect(outside).toEqual([]);
  });

  it("knows the role of every page it holds", () => {
    const unknown = zoneRoutes
      .filter((record) => record.name)
      .map((record) => String(record.name))
      .filter((name) => moderationPageRole(name) === undefined);

    expect(unknown).toEqual([]);
  });

  it.each(MODERATION_SECTIONS.map((section) => [section.label, section.name]))(
    "opens %s at a page of the zone named by the same words",
    (label, name) => {
      const resolved = router.resolve({ name });

      expect(resolved.path.startsWith("/moderation/"), resolved.path).toBe(
        true,
      );
      // The menu row and the tab say one thing: "Запросы на смену имени" was
      // one name in the strip and a longer one in the tab.
      expect(resolved.meta.title).toBe(label);
    },
  );
});

/**
 * The catalogue of schema templates and the page that writes one, the same
 * prefix problem one section over: "new" is a literal segment competing with
 * ":id", and the page behind both addresses is one file that tells them apart
 * by whether the identifier arrived.
 *
 * The names are load-bearing beyond the address bar. The catalogue points a row
 * at the template page by name, the template page pushes back to the catalogue
 * by name after a write, and vue-router answers a name it does not know by
 * throwing: a rename takes the section off the screen instead of turning a test
 * red.
 */
describe("schema template routes of the moderation section", () => {
  const CASES: Array<[string, string]> = [
    ["/moderation/schema-templates", "moderation-schema-templates"],
    ["/moderation/schema-templates/new", "moderation-schema-template-new"],
    ["/moderation/schema-templates/t-1", "moderation-schema-template"],
  ];

  it.each(CASES)("resolves %s to %s", (path, name) => {
    expect(router.resolve(path).name).toBe(name);
  });

  it("hands the page the identifier the address carries", () => {
    // Which is the whole of what the page has to tell a write that replaces a
    // template from one that adds another.
    expect(router.resolve("/moderation/schema-templates/t-1").params).toEqual({
      id: "t-1",
    });
    expect(router.resolve("/moderation/schema-templates/new").params).toEqual(
      {},
    );
  });
});

/**
 * The static head is what a crawler and the tab before the first paint read,
 * and `afterEach` writes the root route's title over it the moment the bundle
 * boots. Two names for one page is what that produced: the document called the
 * site one thing and the tab renamed it to another a second later.
 * documentHead.spec.ts owns the head's own composition; this is the seam
 * between the head and the route table.
 */
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

/**
 * A page that titles itself out of a route param keeps that title when the
 * fetch fails, so /users/nonexistent drew the 404 page under a tab named after
 * the user who does not exist. The forum shell had the answer already: while an
 * error page is on screen the title comes from the error, because the param is
 * then the name of nobody.
 */
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

/**
 * The surface inventory of docs/SURFACES.md is a walkthrough checklist: the
 * owner opens the addresses in it one at a time. /auth/transfer stood in that
 * list and matched no route, so a pass produced a bug report about a page nobody
 * had built, and every link in the section pointed at port 5174 — the preview
 * port only the e2e run starts — so none of them opened at all.
 *
 * What is asserted is the LINK, not the route template beside it: the link
 * carries seed values (/game/aaaaa, /forum/general/5) and is the thing a click
 * actually opens, so a template that resolves while its seeded form 404s would
 * still be caught. Templates whose parameter has no seed value are written
 * without a link and are deliberately out of scope here.
 *
 * Only that direction is asserted. A page built and never listed is what the
 * walkthrough itself finds, while teaching this test which routes count as pages
 * would restate the route table inside it.
 */
const SURFACES = resolve(CLIENT_SRC, "..", "..", "..", "docs", "SURFACES.md");

/**
 * The path of a link the inventory offers: everything after the dev server.
 * A name that is not Latin is linked percent-encoded ("Аккаунт удален"), and
 * the router decodes it the way the browser hands it over.
 */
const ADDRESS = /http:\/\/localhost:\d+(\/[A-Za-z0-9_.:/%-]*)/g;

/** The server the links of the section point at. */
const SERVER = /http:\/\/localhost:(\d+)/g;

describe("the page inventory", () => {
  const inventory =
    readFileSync(SURFACES, "utf8").split("## Поверхности")[1] ?? "";

  it("is the section this test means", () => {
    // A renamed heading would leave every assertion below reading an empty
    // string and passing without having looked at anything.
    expect(inventory.length).toBeGreaterThan(500);
  });

  it("names only addresses the route table answers", () => {
    const linked = [...inventory.matchAll(ADDRESS)].map((match) => match[1]);
    const dangling = [...new Set(linked)].filter(
      (address) => router.resolve(address).name === "not-found",
    );

    expect(dangling).toEqual([]);
  });

  it("links at the dev server, not at the port only e2e starts", () => {
    const ports = [
      ...new Set([...inventory.matchAll(SERVER)].map((match) => match[1])),
    ];

    expect(ports).toEqual(["5173"]);
  });
});

/**
 * The pages a letter leads to stand alone in the shell.
 *
 * The two sidebars are 330px each and they are declared once, by the record
 * that owns the shell, so every page below it gets them whether it has use for
 * them or not. Between 1001px and 1123px that leaves the content column 285px,
 * and a guest card asks for 352px: the card shrinks and its heading wraps,
 * which the surface does not allow. Below 1000px the shell folds on its own and
 * above 1123px there is room for both, so the flag is the fix for the band in
 * between.
 *
 * The list is written out rather than derived: a new page reached from a letter
 * is a decision about the shell, and it should be made here and not inherited
 * by accident. What the shell draws for the flag is checked where it draws it,
 * in App.spec.ts.
 */
const FROM_LETTER = [
  "activation",
  "confirm-email",
  "reset-password",
  "change-username",
  "two-factor-removal",
  "two-factor-removal-cancel",
];

describe("pages a letter leads to", () => {
  const records = router.getRoutes();

  it("marks exactly the six of them", () => {
    const marked = records
      .filter((record) => record.meta.fromLetter)
      .map((record) => String(record.name))
      .sort();

    expect(marked).toEqual([...FROM_LETTER].sort());
  });
});
