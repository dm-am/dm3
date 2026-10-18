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
 *
 * The map used to send every other 4xx to "не найдено" as well, and the rate
 * limit of the API was one of them: a reader who asked too often was told the
 * address was wrong and sent to check a link that was right.
 */
import { describe, it, expect } from "vitest";
import { errorCodeForStatus, getErrorConfig } from "./errorConfig";

describe("errorCodeForStatus", () => {
  const cases: [number | undefined, number][] = [
    [403, 403],
    [404, 404],
    // A page of its own, and that is the page.
    [400, 400],
    [401, 401],
    [409, 409],
    [429, 429],
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

  it("says the rate limit in its own words, not as a wrong address", () => {
    const page = getErrorConfig(errorCodeForStatus(429));
    expect(page.title).toBe("Слишком много запросов");
    expect(page.description).toBe("Подождите немного и обновите страницу.");
  });

  it("never reads a status it has no page for as not found", () => {
    // Nothing on the wire says the address is wrong; the general page says
    // that something went wrong and no more.
    for (const status of [402, 405, 408, 413, 418, 422]) {
      const code = errorCodeForStatus(status);
      expect(code, `status ${status}`).not.toBe(404);
      expect(getErrorConfig(code).title, `status ${status}`).toBe(
        "Неизвестная ошибка",
      );
    }
  });
});
