/**
 * @vitest-environment jsdom
 */

/**
 * The refusal of a press, written where the press was made.
 *
 * The line has to say what the plate used to say, the refusals the
 * interceptor speaks for included, and the plate must not go up beside it: a
 * refusal said twice, once far from the button, is the defect the owner's rule
 * of 26 September 2026 ends. What it must not take over is the pair that is not
 * about the press - an expired session, a dead connection - whose plate stays.
 */
import { afterEach, describe, expect, it, vi } from "vitest";
import type { GeneralError } from "@/shared/api/models/common";
import { announceWorded } from "@/shared/lib/errors";
import { useActionResult } from "./useActionResult";

const problem = (
  status: number,
  title = "",
  extra: Partial<GeneralError> = {},
): GeneralError => ({ type: "", title, status, traceId: "t", ...extra });

/** Lets a plate the interceptor holds run out. */
const endOfTask = () => new Promise((resolve) => setTimeout(resolve, 0));

afterEach(() => {
  vi.useRealTimers();
});

describe("the refusal of a press at its place", () => {
  it("is empty until a press is refused", () => {
    const result = useActionResult();

    expect(result.refusal.value).toBe("");
    expect(result.waiting.value).toBe(false);
  });

  it("says the server's own sentence, or the fallback where it named nothing", () => {
    const result = useActionResult();

    result.refuse(problem(409, "Уже идет другой эвент"), "Не удалось начать");
    expect(result.refusal.value).toBe("Уже идет другой эвент");

    result.refuse(problem(404), "Не удалось начать");
    expect(result.refusal.value).toBe("Не удалось начать");
  });

  it("names a refused field by its label on the screen", () => {
    const result = useActionResult();

    result.refuse(
      {
        ...problem(400, "One or more validation errors occurred."),
        errors: { Text: ["Long"] },
      },
      "Не удалось сохранить",
      { text: "Текст комментария" },
    );

    expect(result.refusal.value).toBe(
      "Текст комментария: слишком длинное значение",
    );
  });

  it("empties on the next press", () => {
    const result = useActionResult();
    result.refuse(problem(404), "Не удалось сохранить");

    result.clear();

    expect(result.refusal.value).toBe("");
  });

  it.each([
    ["the server's own sentence", 403, "Вы в черном списке этого блога"],
    ["the general refusal", 403, "Недостаточно прав для этого действия"],
    ["a server error", 500, "Ошибка сервера"],
  ])(
    "takes down the plate the interceptor held for %s",
    async (_case, status, title) => {
      const failure = problem(status, title);
      const raise = vi.fn();
      announceWorded(failure, raise);

      const result = useActionResult();
      result.refuse(failure, "Не удалось сохранить");
      await endOfTask();

      expect(result.refusal.value).toBe(title);
      expect(raise).not.toHaveBeenCalled();
    },
  );

  it.each([
    ["an expired session", 401],
    ["a request that never arrived", 0],
  ])("leaves %s to its plate and says nothing", async (_case, status) => {
    const failure = problem(status);
    const raise = vi.fn();
    announceWorded(failure, raise);

    const result = useActionResult();
    result.refuse(failure, "Не удалось сохранить");
    await endOfTask();

    expect(result.refusal.value).toBe("");
    expect(raise).toHaveBeenCalledTimes(1);
  });

  it("counts a rate limit down in the line and keeps the control shut", () => {
    vi.useFakeTimers();
    const result = useActionResult();

    result.refuse(problem(429, "", { retryAfterSeconds: 3 }), "Не удалось");

    expect(result.refusal.value).toBe(
      "Слишком много попыток, попробуйте через 3 секунды",
    );
    expect(result.waiting.value).toBe(true);

    vi.advanceTimersByTime(3000);

    expect(result.refusal.value).toBe("");
    expect(result.waiting.value).toBe(false);
  });
});
