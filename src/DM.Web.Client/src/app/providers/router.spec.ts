/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import type { RouteLocationNormalized, RouteLocationRaw } from "vue-router";
/** Who the session cookie belongs to: a guest unless a test says otherwise. */
const whoAmI = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/api/personalApi", () => ({
  // The page sizes the session reads with it have no say in where a guard sends.
  default: {
    getMyProfile: whoAmI,
    getMyPreferences: async () => ({ data: null, error: null }),
  },
}));

import router, { guardAuthenticated } from "./router";
import { REDIRECT_QUERY_KEY } from "@/shared/lib/auth";

/** A sample of every zone: the shell and the zone flags of each. */
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

  // The messenger lays its list where the right column stands.
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

  // The post pair and the game pair of reviews read alike.
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

  // The game shell at the same depth stays mounted between the room and the game.
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

  // A real route of the table; the guard reads only meta and fullPath of it.
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

// A literal segment ("create", "new") competes with a parameter, and links and
// pushes point at the names.
describe("addresses that share a prefix", () => {
  it.each([
    ["/game/abcde/characters", "game-characters"],
    ["/game/abcde/characters/create", "game-character-create"],
    ["/game/abcde/characters/7f1c/edit", "game-character-edit"],
    ["/game/abcde/characters/7f1c", "game-character"],
    ["/blogs/abcde/feed", "blog-feed"],
    ["/blogs/abcde/feed/create", "blog-publication-create"],
    ["/blogs/abcde/feed/7f1c", "blog-publication"],
    ["/blogs/abcde/feed/7f1c/edit", "blog-publication-edit"],
    ["/publication/7f1c", "publication-redirect"],
    ["/moderation/schema-templates", "moderation-schema-templates"],
    ["/moderation/schema-templates/new", "moderation-schema-template-new"],
    ["/moderation/schema-templates/t-1", "moderation-schema-template"],
  ])("resolves %s to %s", (path, name) => {
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

  it("hands the template page the identifier the address carries", () => {
    expect(router.resolve("/moderation/schema-templates/t-1").params).toEqual({
      id: "t-1",
    });
    expect(router.resolve("/moderation/schema-templates/new").params).toEqual(
      {},
    );
  });
});

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

// The notification of the answer leads to /my-tickets/{id}.
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

// Written out, not derived: a new page a letter leads to is a decision about
// the shell (App.spec.ts checks what the shell draws for the flag).
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
