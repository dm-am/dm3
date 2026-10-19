/**
 * @vitest-environment node
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
    // Only the topic endpoint spends 410; a stray one must not claim a deletion.
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
    for (const status of [402, 405, 408, 413, 418, 422]) {
      const code = errorCodeForStatus(status);
      expect(code, `status ${status}`).not.toBe(404);
      expect(getErrorConfig(code).title, `status ${status}`).toBe(
        "Неизвестная ошибка",
      );
    }
  });
});
