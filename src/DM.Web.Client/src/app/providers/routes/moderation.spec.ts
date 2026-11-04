/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import type { RouteLocationRaw } from "vue-router";

import router from "../router";
import {
  MODERATION_SECTIONS,
  moderationPageRole,
} from "@/shared/config/moderationSections";

/** The pages of the zone open to everyone. */
const PUBLIC = ["/moderation/bans"];

/** A route record's address with its parameters filled in. */
const addressOf = (path: string) => path.replace(/:\w+/g, "x");

// A section declared beside the zone would miss its sign-in, menu and 403.
describe("the moderation zone", () => {
  const zoneRoutes = router
    .getRoutes()
    .filter((record) => record.path.startsWith("/moderation/"));

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
          (resolved.meta.requiresAuth !== true && !PUBLIC.includes(path))
        );
      });

    expect(outside).toEqual([]);
  });

  // The public record of the site: read by everyone, an expired session
  // leaves the reader on it (sessionExpiry.ts reads requiresAuth).
  it("opens the ban list, the warning log and the pulse to a guest", () => {
    for (const path of [...PUBLIC, "/warnings", "/pulse"]) {
      expect(router.resolve(path).meta.requiresAuth, path).toBeFalsy();
    }
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
      expect(resolved.meta.title).toBe(label);
    },
  );
});
