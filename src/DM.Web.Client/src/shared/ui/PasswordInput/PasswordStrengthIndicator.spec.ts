import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import PasswordStrengthIndicator from "./PasswordStrengthIndicator.vue";
import type { HibpStatus } from "./PasswordStrengthIndicator.vue";

/**
 * The single line under the bar. The breach lookup had no branch in it at all:
 * while the request was out the line went on naming a strength, and the only
 * sign of the wait was a submit button that had gone dead.
 */
const lineUnderTheBar = (password: string, hibpStatus: HibpStatus) =>
  mount(PasswordStrengthIndicator, { props: { password, hibpStatus } }).text();

describe("PasswordStrengthIndicator", () => {
  it("names the breach lookup while it is out", () => {
    expect(lineUnderTheBar("correct horse", "checking")).toBe(
      "Проверяем по базе утечек...",
    );
  });

  it("goes back to the strength once the lookup answers", () => {
    expect(lineUnderTheBar("correct horse", "safe")).toBe("Надежный");
  });

  it("keeps a rule the field itself broke above the lookup", () => {
    expect(lineUnderTheBar("short", "checking")).toBe("Минимум 8 символов");
  });

  it("says so when the lookup answers that the password is in a breach", () => {
    expect(lineUnderTheBar("correct horse", "compromised")).toBe(
      "Пароль найден в утечках данных",
    );
  });
});
