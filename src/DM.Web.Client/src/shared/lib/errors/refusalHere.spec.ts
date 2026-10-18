/**
 * The sentence a page writes at the place of a press, for the places that keep
 * their own line: the row of a list, a field of a form.
 */
import { describe, expect, it, vi } from "vitest";
import type { GeneralError } from "@/shared/api/models/common";
import { announceWorded } from "./generalPlate";
import { refusalHere } from "./refusalHere";

const problem = (
  status: number,
  title = "",
  extra: Partial<GeneralError> = {},
): GeneralError => ({ type: "", title, status, traceId: "t", ...extra });

const endOfTask = () => new Promise((resolve) => setTimeout(resolve, 0));

describe("refusalHere", () => {
  it("says the server's sentence and takes down the plate held for it", async () => {
    const failure = problem(403, "Вы в черном списке этого блога");
    const raise = vi.fn();
    announceWorded(failure, raise);

    expect(refusalHere(failure, "Не удалось")).toBe(
      "Вы в черном списке этого блога",
    );
    await endOfTask();
    expect(raise).not.toHaveBeenCalled();
  });

  it("says the wait a rate limit named", () => {
    expect(
      refusalHere(problem(429, "", { retryAfterSeconds: 30 }), "Не удалось"),
    ).toBe("Слишком много попыток, попробуйте через 30 секунд");
  });

  it("falls back on the phrase of the place where the server named nothing", () => {
    expect(refusalHere(problem(404), "Не удалось отменить подписку")).toBe(
      "Не удалось отменить подписку",
    );
  });

  it.each([401, 0])(
    "leaves status %i to its plate and gives no line",
    async (status) => {
      const failure = problem(status);
      const raise = vi.fn();
      announceWorded(failure, raise);

      expect(refusalHere(failure, "Не удалось")).toBeNull();
      await endOfTask();
      expect(raise).toHaveBeenCalledTimes(1);
    },
  );
});
