/**
 * @vitest-environment node
 */

import { describe, it, expect } from "vitest";
import { optionsHint, ROLE_OPTIONS } from "../model";

describe("users filter hints", () => {
  it("names every role the filter offers", () => {
    expect(optionsHint(ROLE_OPTIONS)).toBe(
      "Пользователь, Наставник, Модератор, Старший модератор, Администратор",
    );
  });
});
