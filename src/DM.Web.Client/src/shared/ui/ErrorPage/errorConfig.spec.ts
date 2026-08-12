/**
 * @vitest-environment node
 */

/**
 * The status-to-page map, as a table.
 *
 * There were three copies of it and they had already diverged. The forum board
 * page turned everything but 404 into "ошибка сервера", and a missing board was
 * answered with 410 back then, so that is what a mistyped alias actually drew;
 * the topic page next to it read the same 410 as "Страница удалена"; the
 * profile page had a third spelling. Games and blogs had no map at all and drew
 * one paragraph for every failure.
 *
 * 410 now carries one meaning on the wire, a resource that was deleted, and only
 * the topic endpoint spends it. Both readings stay pinned here rather than left
 * to whoever writes the fourth copy: the default has to survive a 410 from an
 * endpoint that has no business sending one.
 */
import { describe, it, expect } from "vitest";
import { errorCodeForStatus, getErrorConfig } from "./errorConfig";

describe("errorCodeForStatus", () => {
  const cases: [number | undefined, number][] = [
    [403, 403],
    [404, 404],
    // No page of its own: "not found" is the only honest thing left to say.
    [400, 404],
    [401, 404],
    [409, 404],
    [500, 500],
    [502, 500],
    [503, 500],
    // A request that never reached the server carries no status.
    [undefined, 500],
  ];

  for (const [status, code] of cases) {
    it(`maps ${status ?? "no status"} to ${code}`, () => {
      expect(errorCodeForStatus(status)).toBe(code);
      expect(errorCodeForStatus(status, { goneMeansRemoved: true })).toBe(code);
    });
  }

  it("reads Gone as not-found by default", () => {
    // Boards, users and games answer 404 now, so a Gone from anywhere but the
    // topic endpoint is a stray one and must not claim a deletion.
    expect(errorCodeForStatus(410)).toBe(404);
  });

  it("reads Gone as removed where the endpoint means removed", () => {
    // Only the topic endpoint tells a deleted topic from one that never was.
    expect(errorCodeForStatus(410, { goneMeansRemoved: true })).toBe(410);
  });

  it("only ever names a code the error page has a page for", () => {
    const produced = new Set(
      [undefined, 400, 401, 403, 404, 409, 410, 418, 500, 503].flatMap((s) => [
        errorCodeForStatus(s),
        errorCodeForStatus(s, { goneMeansRemoved: true }),
      ]),
    );
    for (const code of produced) {
      // getErrorConfig falls back to a generic sentence for anything unknown,
      // and an error page that says "Неизвестная ошибка" is the outcome this
      // map exists to avoid.
      expect(getErrorConfig(code).title).not.toBe("Неизвестная ошибка");
    }
  });
});
