/**
 * @vitest-environment node
 */

// The countdown is rewritten every second, so the noun is read at every value.
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

  // The lockout is half an hour: past a minute and a half the wait is said in
  // minutes, rounded up, never shorter than the real one.
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

  // No header: a wait the client invented would be a guess.
  it("names no time when the server asked for none", () => {
    expect(describeRateLimit()).toBe("Слишком много попыток, попробуйте позже");
    expect(describeRateLimit(0)).toBe(
      "Слишком много попыток, попробуйте позже",
    );
  });
});
