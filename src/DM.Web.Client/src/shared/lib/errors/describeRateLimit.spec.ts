/**
 * @vitest-environment node
 */

/**
 * The countdown a guest form stands in its own strip is rewritten every second
 * it runs, so the noun after the number is read at every one of its values. A
 * single spelling said "через 3 секунд" on the way in — the one screen a person
 * retries on purpose, and therefore the one where the sentence is actually read
 * to the end.
 */
import { describe, expect, it } from "vitest";
import { describeRateLimit } from "./describeRateLimit";

describe("the sentence of a rate limit", () => {
  it("agrees with the number of seconds it names", () => {
    expect(describeRateLimit(1)).toBe(
      "Слишком много попыток, попробуйте через 1 секунду",
    );
    expect(describeRateLimit(2)).toBe(
      "Слишком много попыток, попробуйте через 2 секунды",
    );
    expect(describeRateLimit(5)).toBe(
      "Слишком много попыток, попробуйте через 5 секунд",
    );
    // The teens take the third form whatever their last digit says.
    expect(describeRateLimit(11)).toBe(
      "Слишком много попыток, попробуйте через 11 секунд",
    );
    expect(describeRateLimit(21)).toBe(
      "Слишком много попыток, попробуйте через 21 секунду",
    );
  });

  /**
   * The lockout after fifteen wrong passwords is half an hour, and it comes
   * through this same sentence. "Через 1800 секунд" is a number the reader has
   * to divide before it means anything, so past a minute and a half the wait is
   * said in minutes and rounded up: a sentence that names a shorter wait than
   * the real one sends the reader back into a door that is still shut.
   */
  it("says a long wait in minutes", () => {
    expect(describeRateLimit(1800)).toBe(
      "Слишком много попыток, попробуйте через 30 минут",
    );
    expect(describeRateLimit(61)).toBe(
      "Слишком много попыток, попробуйте через 61 секунду",
    );
    expect(describeRateLimit(91)).toBe(
      "Слишком много попыток, попробуйте через 2 минуты",
    );
    expect(describeRateLimit(120)).toBe(
      "Слишком много попыток, попробуйте через 2 минуты",
    );
    expect(describeRateLimit(121)).toBe(
      "Слишком много попыток, попробуйте через 3 минуты",
    );
    expect(describeRateLimit(3600)).toBe(
      "Слишком много попыток, попробуйте через 60 минут",
    );
  });

  // No header, no number: the sentence then names no time, because a wait this
  // side invented would be a guess enforced on the reader.
  it("names no time when the server asked for none", () => {
    expect(describeRateLimit()).toBe("Слишком много попыток, попробуйте позже");
    expect(describeRateLimit(0)).toBe(
      "Слишком много попыток, попробуйте позже",
    );
  });
});
